using Europeana3D.Web.Models;
using Europeana3D.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Europeana3D.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ViewerService _viewers;
        private readonly BridgeService _bridge;
        private readonly S3Service _s3;
        private readonly ZenodoService _zenodo;
        private readonly RepositoryService _repositories;
        private readonly ZenodoCsvService _csv;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public HomeController(ViewerService viewers, BridgeService bridge, S3Service s3, ZenodoService zenodo,
            RepositoryService repositories, ZenodoCsvService csv, IConfiguration configuration, IWebHostEnvironment env)
        {
            _viewers = viewers;
            _bridge = bridge;
            _s3 = s3;
            _zenodo = zenodo;
            _repositories = repositories;
            _csv = csv;
            _configuration = configuration;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            var supportedPattern = string.Join(",", (_configuration["Options:SupportedFormats"] ?? "obj|ifc|dae|glb").Split("|").Select(p => $".{p}"));
            ViewData["supportedExt"] = supportedPattern;
            ViewData["viewers"] = (await _viewers.LoadViewersAsync()).Select(v => new ViewerOption(v.ProviderID, $"{v.ServiceName}", v.Protocols.Contains("oEmbed"))).ToList();
            ViewData["repositories"] = _repositories.GetAll();
            return View();
        } 

        // CHANGED: added repo and bucket parameters
        [HttpGet]    
        public async Task<IActionResult> Search(string q, string repo, string bucket, string apikey) // [ADDED for S3 and Zenodo integration]
        {
            if (string.IsNullOrWhiteSpace(q)) return RedirectToAction("Index");

            List<EuropeanaItem> items;

            // [ADDED for S3 integration] branch logic
            if (!string.IsNullOrWhiteSpace(repo) &&
                repo.Equals("S3", StringComparison.OrdinalIgnoreCase))
            {
                // Search in Amazon S3
                items = await _s3.Search3DInBucketAsync(bucket ?? string.Empty, q);
                ViewData["repo"] = "S3";
            }
            // [ADDED for Zenodo integration] Zenodo branch
            else if (!string.IsNullOrWhiteSpace(repo) &&
                     repo.Equals("Zenodo", StringComparison.OrdinalIgnoreCase))
            {
                items = await _zenodo.Search3DAsync(q, apikey ?? string.Empty); // [ADDED for Zenodo integration]
                ViewData["repo"] = "Zenodo";
                ViewData["accessToken"] = apikey;
            }
            else
            {
                // Default / Europeana path (existing behavior)
                items = await _bridge.SearchModelXML(q);
                ViewData["repo"] = "Europeana";
            }

            return View("SearchResults", items);
        }

        [HttpGet]
        public async Task<IActionResult> ChooseAction(string id, string title, string preview, string url, string extension, long? size, string? zenodoRecordId, string? accessToken)
        {
            var model = new ChooseActionViewModel
            {
                EuropeanaId = id,
                Title = title,
                Preview = preview,
                FileExtension = extension,
                SupportedUrls = new List<string> { url },
                SelectedUrl = url,
                FileSize = size,
                ZenodoRecordId = zenodoRecordId,
                AccessToken = accessToken,
                Viewers = (await _viewers.LoadViewersAsync(extension))
                    .Select(v => new ViewerOption(v.ProviderID, $"{v.ServiceName}", v.Protocols.Contains("oEmbed")))
                    .ToList()
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CheckMetadata(string zenodoRecordId, string? accessToken = null)
        {
            var exists = await _csv.MetadataCsvExistsAsync(zenodoRecordId, accessToken);
            return Json(exists);
        }

        [HttpGet]
        public async Task<IActionResult> ModelMetadata(string zenodoRecordId, string pid, string? accessToken = null, bool landingPage = false)
        {
            var csvContent = await _csv.FetchCsvAsync(zenodoRecordId, accessToken);
            if (csvContent == null)
                return View(new ModelMetadataViewModel
                {
                    Pid = pid,
                    ZenodoRecordId = zenodoRecordId,
                    ErrorMessage = "Metadata CSV not found in this Zenodo record."
                });

            if (landingPage)
            {
                var raw = _csv.FindRowRaw(csvContent, pid);
                if (raw == null)
                    return View(new ModelMetadataViewModel
                    {
                        Pid = pid,
                        ZenodoRecordId = zenodoRecordId,
                        ErrorMessage = $"No metadata entry found for PID '{pid}'."
                    });

                var meta = BuildMeta(raw.Value.Headers, raw.Value.Values);
                var jsonOpts = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                var metaJson = JsonSerializer.Serialize(meta, jsonOpts);

                // EN title
                var titleField = _configuration["Options:CsvTitleField"] ?? "dc:title (object name)";
                var titleEntry = meta.FirstOrDefault(m => m.TryGetValue("k", out var k) && string.Equals(k, titleField, StringComparison.OrdinalIgnoreCase));
                string? rowTitle = null;
                if (titleEntry != null)
                {
                    titleEntry.TryGetValue("en", out rowTitle);
                    if (string.IsNullOrEmpty(rowTitle)) titleEntry.TryGetValue("v", out rowTitle);
                }

                // model URL + extension (for the hidden viewer form)
                var modelUrlField = _configuration["Options:CsvModelUrlField"] ?? "edm:isShownBy";
                var modelUrlEntry = meta.FirstOrDefault(m => m.TryGetValue("k", out var k) && string.Equals(k, modelUrlField, StringComparison.OrdinalIgnoreCase));
                string? modelUrl = null;
                modelUrlEntry?.TryGetValue("v", out modelUrl);
                // Path.GetExtension doesn't work on Zenodo URLs ending in /content — walk segments
                string ext = "";
                if (!string.IsNullOrEmpty(modelUrl))
                {
                    try
                    {
                        var uri = new Uri(modelUrl);
                        foreach (var seg in uri.Segments.Reverse())
                        {
                            var e = Path.GetExtension(seg.TrimEnd('/'));
                            if (!string.IsNullOrEmpty(e)) { ext = e.ToLowerInvariant(); break; }
                        }
                    }
                    catch { ext = Path.GetExtension(modelUrl).ToLowerInvariant(); }
                }

                // viewers for that format
                var allViewers = await _viewers.LoadViewersAsync(ext);
                var viewerOptions = allViewers
                    .Select(v => new ViewerOption(v.ProviderID, v.ServiceName, v.Protocols.Contains("oEmbed")))
                    .ToList();

                // Build FIELDS map: LandingPage section + title/pid/model from Options
                var landingSection = _configuration.GetSection("LandingPage").GetChildren()
                    .ToDictionary(c => c.Key, c => c.Value ?? "");
                landingSection["title"] = _configuration["Options:CsvTitleField"]   ?? "dc:title (object name)";
                landingSection["pid"]   = _configuration["Options:CsvPidField"]      ?? "edm:pid";
                landingSection["model"] = _configuration["Options:CsvModelUrlField"] ?? "edm:isShownBy";
                var landingFieldsJson = JsonSerializer.Serialize(landingSection,
                    new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

                return View(new ModelMetadataViewModel
                {
                    Pid = pid,
                    Title = rowTitle,
                    ZenodoRecordId = zenodoRecordId,
                    LandingPage = true,
                    MetaJson = metaJson,
                    ModelUrl = modelUrl,
                    FileExtension = ext,
                    Viewers = viewerOptions,
                    LandingFieldsJson = landingFieldsJson
                });
            }

            // ── simple table view (unchanged) ─────────────────────────────────
            var rows = _csv.ParseCsv(csvContent);
            var row = _csv.FindRowByPid(rows, pid);
            if (row == null)
                return View(new ModelMetadataViewModel
                {
                    Pid = pid,
                    ZenodoRecordId = zenodoRecordId,
                    ErrorMessage = $"No metadata entry found for PID '{pid}'."
                });

            var titleFieldSimple = _configuration["Options:CsvTitleField"] ?? "dc:title";
            row.TryGetValue(titleFieldSimple, out var rowTitleSimple);
            var thumbUrl = await _csv.ProbeThumbAsync(zenodoRecordId, pid, accessToken);

            var fields = row
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => (Column: kv.Key, Value: kv.Value))
                .ToList();

            return View(new ModelMetadataViewModel
            {
                Pid = pid,
                Title = rowTitleSimple,
                ThumbUrl = thumbUrl,
                ZenodoRecordId = zenodoRecordId,
                Fields = fields
            });
        }

        private static (string? lang, string @base) NormHeader(string h)
        {
            if (h.Contains(" en (")) return ("en", h.Replace(" en (", " ("));
            if (h.Contains(" hr (")) return ("hr", h.Replace(" hr (", " ("));
            if (h.EndsWith(" en"))   return ("en", h[..^3]);
            if (h.EndsWith(" hr"))   return ("hr", h[..^3]);
            return (null, h);
        }

        private static List<Dictionary<string, string>> BuildMeta(string[] headers, string[] values)
        {
            var order = new List<string>();
            var cols = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                var (lang, bas) = NormHeader(headers[i]);
                if (!cols.ContainsKey(bas)) { cols[bas] = new(); order.Add(bas); }
                cols[bas][lang ?? "v"] = i;
            }
            var result = new List<Dictionary<string, string>>();
            foreach (var bas in order)
            {
                var c = cols[bas];
                if (c.Count == 1 && c.ContainsKey("v"))
                {
                    var v = c["v"] < values.Length ? values[c["v"]].Trim() : "";
                    if (v.Length > 0) result.Add(new() { ["k"] = bas, ["v"] = v });
                }
                else
                {
                    var en = c.ContainsKey("en") && c["en"] < values.Length ? values[c["en"]].Trim() : "";
                    var hr = c.ContainsKey("hr") && c["hr"] < values.Length ? values[c["hr"]].Trim() : "";
                    if (en.Length > 0 || hr.Length > 0)
                        result.Add(new() { ["k"] = bas, ["en"] = en, ["hr"] = hr });
                }
            }
            return result;
        }

        [HttpPost]
        public async Task<IActionResult> ChooseAction(ChooseActionViewModel model)
        {
            if (model.SelectedAction == "download")
                return Redirect(model.SelectedUrl!);

            var allViewers = await _viewers.LoadViewersAsync(model.FileExtension);
            model.Viewers = allViewers
                .Select(v => new ViewerOption(v.ProviderID, v.ServiceName, v.Protocols.Contains("oEmbed")))
                .ToList();

            var viewer = allViewers.FirstOrDefault(v => v.ProviderID == model.SelectedViewerProviderId);
            if (viewer == null) return View("Result", $"Viewer not found: {model.SelectedViewerProviderId}");

            var (ok, msg) = await _viewers.PostModelLoadingAsync(model.SelectedUrl!, null, viewer, model.FileSize, model.FileExtension, model.SelectedAction);
            ViewData["viewer"] = viewer.ServiceName;
            ViewData["error"] = ok ? string.Empty : msg;
            return View("Result", msg);
        }


        [HttpPost]                                                  
        public async Task<IActionResult> UploadModel(IFormFile file, string modelname, string? description, string? SelectedViewerProviderId, string? SelectedAction)            
        {
            if (file == null || file.Length == 0)
                return View("Result", "No file selected.");        

            // Validate extension against Options:SupportedFormats
            var supportedPattern = _configuration["Options:SupportedFormats"]; 
            var regexExt = new Regex(@"\.(" + supportedPattern + ")$", RegexOptions.IgnoreCase);  

            var originalName = Path.GetFileName(file.FileName);  
            if (!regexExt.IsMatch(originalName))
                return View("Result", $"Unsupported file type. Allowed: {supportedPattern}");

            var extension = Path.GetExtension(originalName).TrimStart('.').ToLowerInvariant();
            long size = file.Length;

            var viewer = (await _viewers.LoadViewersAsync()).FirstOrDefault(v => v.ProviderID == SelectedViewerProviderId);
            if (viewer == null) return View("Result", $"Viewer not found: {SelectedViewerProviderId}");

            byte[] bytes;
            using (var ms = new MemoryStream())               
            {
                file.CopyTo(ms);
                bytes = ms.ToArray();
            }

            string base64 = Convert.ToBase64String(bytes);

            var fileContent = new FileContent { Name = modelname, Filename = originalName!, Description = description, Base64Data = base64 };

            var (ok, msg) = await _viewers.PostModelLoadingAsync(null, fileContent, viewer, size, extension, SelectedAction);
            ViewData["viewer"] = viewer.ServiceName;
            ViewData["error"] = ok ? string.Empty : msg;
            return View("Result", msg);
        }
    }

}