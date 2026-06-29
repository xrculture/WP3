using Europeana3D.Web.Models;
using Europeana3D.Web.Services;
using Microsoft.AspNetCore.Mvc;
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
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public HomeController(ViewerService viewers, BridgeService bridge, S3Service s3, ZenodoService zenodo,
            RepositoryService repositories, IConfiguration configuration, IWebHostEnvironment env)
        {
            _viewers = viewers;
            _bridge = bridge;
            _s3 = s3;
            _zenodo = zenodo;
            _repositories = repositories;
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
        public async Task<IActionResult> ChooseAction(string id, string title, string preview, string url, string extension, long? size)
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
                Viewers = (await _viewers.LoadViewersAsync(extension))
                    .Select(v => new ViewerOption(v.ProviderID, $"{v.ServiceName}", v.Protocols.Contains("oEmbed")))
                    .ToList()
            };
            return View(model);
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