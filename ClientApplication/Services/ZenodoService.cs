using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Europeana3D.Web.Models;
using System.Net;
using System.Xml.Serialization;
using System.Xml.Linq;
using Microsoft.AspNetCore.WebUtilities;

namespace Europeana3D.Web.Services
{
    // [ADDED for Zenodo integration]
    public class ZenodoService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly EuropeanaService _europeana;
        private readonly XmlTemplateService _xml;

        // We assume HttpClientFactory is already available via AddHttpClient()
        public ZenodoService(IHttpClientFactory httpClientFactory, EuropeanaService europeana, XmlTemplateService xml, IConfiguration configuration) // [ADDED for Zenodo integration]
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _europeana = europeana;
            _xml = xml;
        }

        /// <summary>
        /// Search Zenodo for public records matching the query.
        /// Extract files with supported 3D extensions and return them
        /// as EuropeanaItem objects so downstream views stay unchanged.
        /// </summary>
        public async Task<List<EuropeanaItem>> Search3DAsync(string query, string apikey) // [ADDED for Zenodo integration]
        {

            // UI to Middleware for SearchRequest
            var tmpl = _xml.LoadModelRequestTemplate();
            var root = tmpl.Root!;

            var filters = root.Element("Filters")!
            .Element("Filter")!;
            filters.Element("SearchQuery")!.Value = query;
            filters.Element("AccessToken")!.Value = apikey;

            root.Element("ServiceUrl")!.Value = "https://zenodo.org/api/records/";

            // --- Save the finalized XML to Resources before making the HTTP call ---
            _xml.SaveLocalXML(tmpl, Path.Combine(AppContext.BaseDirectory, "Resources"), "ModelRequest");

            // Send request to Zenodo
            var xml = await ProcessAsync(tmpl);

            // Middleware to UI for SearchResponse
            var serializer = new XmlSerializer(typeof(ModelResponse));
            using var sr = new StringReader(xml.ToString());
            var items = (ModelResponse)serializer.Deserialize(sr)!;

            return items.EuropeanaItems;
        }


        public async Task<XDocument> ProcessAsync(XDocument requestXml)
        {
            // Connector Middleware to Zenodo for SearchRequest

            var modelReq = requestXml.Root!;

            var serviceUrl = (string?)modelReq.Element("ServiceUrl") ?? throw new InvalidOperationException("ServiceUrl not found");
            var pagination = modelReq.Element("Pagination");
            var lengthAttr = pagination?.Attribute("length")?.Value;
            var rows = int.TryParse(lengthAttr, out var len) ? len : 100;


            var filters = modelReq.Element("Filters")?.Elements("Filter").FirstOrDefault();
            var searchQuery = filters?.Element("SearchQuery")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(searchQuery))
                throw new InvalidOperationException("Search Query not found");

            var token = filters?.Element("AccessToken")?.Value ?? string.Empty;

            var output = new List<EuropeanaItem>();
            var response = new ModelResponse();

            // Build Zenodo API URL.
            // NOTE: not assuming an access token. Public search.
            // We request basic metadata and files list for hits.
            // Zenodo API classic: `https://zenodo.org/api/records/?q=...`

            var encodedQ = Uri.EscapeDataString(searchQuery);
            // Zenodo public API caps page size at 25; authenticated requests allow up to 100
            var pageSize = string.IsNullOrEmpty(token) ? 25 : 100;
            var requestUrl = $"{serviceUrl}?q={encodedQ}&size={pageSize}";
            if (!string.IsNullOrEmpty(token))
                requestUrl += $"&access_token={token}";


            using var resp = await _httpClient.GetAsync(requestUrl);
            if (!resp.IsSuccessStatusCode)
            {
                var errStatus = (int)resp.StatusCode;
                response = new ModelResponse { Status = errStatus, Message = ReasonPhrases.GetReasonPhrase(errStatus), EuropeanaItems = output };
                var ser0 = new XmlSerializer(typeof(ModelResponse));
                using var sw0 = new StringWriter();
                ser0.Serialize(sw0, response);
                return XDocument.Parse(sw0.ToString());
            }

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            // We reuse SupportedFormats from config
            var supportedPattern = _configuration["Options:SupportedFormats"];
            var regexExt = new Regex(@"\.(" + supportedPattern + ")$", RegexOptions.IgnoreCase);
            var previewRegexExt = new Regex(@"\.(jpeg|jpg|png|bmp)$", RegexOptions.IgnoreCase);

            // Zenodo returns hits either as root.hits.hits[] (legacy) or root.hits[] (v2).
            if (!doc.RootElement.TryGetProperty("hits", out var hitsRoot))
            {
                response = new ModelResponse { Status = 200, Message = "No results", EuropeanaItems = output };
                var ser1 = new XmlSerializer(typeof(ModelResponse));
                using var sw1 = new StringWriter();
                ser1.Serialize(sw1, response);
                return XDocument.Parse(sw1.ToString());
            }

            JsonElement hitsArray;
            if (hitsRoot.ValueKind == JsonValueKind.Array)
                hitsArray = hitsRoot;
            else if (hitsRoot.TryGetProperty("hits", out var nested) && nested.ValueKind == JsonValueKind.Array)
                hitsArray = nested;
            else
            {
                response = new ModelResponse { Status = 200, Message = "No results", EuropeanaItems = output };
                var ser2 = new XmlSerializer(typeof(ModelResponse));
                using var sw2 = new StringWriter();
                ser2.Serialize(sw2, response);
                return XDocument.Parse(sw2.ToString());
            }

            foreach (var hit in hitsArray.EnumerateArray())
            {
                // We'll gather candidate files first
                var fileCandidates = new List<FileCandidate>();
                var previewUrls = new List<string>();
                string? recordId = null;
                string? title = null;

                // Extract record id
                if (hit.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
                {
                    recordId = idProp.ToString();
                }

                // Extract title from metadata.title
                if (hit.TryGetProperty("metadata", out var metadataProp) &&
                    metadataProp.ValueKind == JsonValueKind.Object)
                {
                    if (metadataProp.TryGetProperty("title", out var titleProp) &&
                        titleProp.ValueKind == JsonValueKind.String)
                    {
                        title = titleProp.GetString();
                    }
                }

                // Extract files
                if (hit.TryGetProperty("files", out var filesProp) &&
                    filesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var f in filesProp.EnumerateArray())
                    {
                        // expected properties: key (filename), links.download, size
                        string? fname = null;
                        string? downloadUrl = null;
                        long? size = null;

                        if (f.TryGetProperty("key", out var keyProp) &&
                            keyProp.ValueKind == JsonValueKind.String)
                        {
                            fname = keyProp.GetString();
                        }

                        if (f.TryGetProperty("size", out var sizeProp) &&
                            sizeProp.ValueKind == JsonValueKind.Number)
                        {
                            if (sizeProp.TryGetInt64(out var sz))
                                size = sz;
                        }

                        if (f.TryGetProperty("links", out var linksProp) &&
                            linksProp.ValueKind == JsonValueKind.Object)
                        {
                            if (linksProp.TryGetProperty("self", out var dlProp) &&
                                dlProp.ValueKind == JsonValueKind.String)
                            {
                                downloadUrl = dlProp.GetString();
                            }
                        }

                        if (!string.IsNullOrEmpty(fname)
                                && previewRegexExt.IsMatch(fname ?? string.Empty)
                                && !string.IsNullOrEmpty(downloadUrl))
                        {
                            previewUrls.Add(downloadUrl);
                        }

                        // Only take files matching our allowed 3D extensions
                        if (!string.IsNullOrEmpty(fname) &&
                            regexExt.IsMatch(fname ?? string.Empty) &&
                            !string.IsNullOrEmpty(downloadUrl))
                        {
                            // Build candidate from Zenodo metadata directly; ValidateAsync
                            // may fail on Zenodo CDN redirects/rate-limits so we don't gate on it.
                            var fc = await _europeana.ValidateAsync(downloadUrl, default)
                                     ?? new FileCandidate { Url = downloadUrl!, ContentLength = size };

                            if (string.IsNullOrEmpty(fc.Extension))
                                fc.Extension = Path.GetExtension(fname);

                            fileCandidates.Add(fc);
                        }
                    }
                }

                // Only add an item if we found at least one 3D file
                if (fileCandidates.Any())
                {
                    output.Add(new EuropeanaItem
                    {
                        Id = recordId ?? Guid.NewGuid().ToString(),
                        Title = title ?? recordId ?? "(untitled Zenodo record)",
                        Previews = previewUrls.Any() ? previewUrls : new List<string> { "images/3dmodel.png" },
                        Supported3DFiles = fileCandidates
                    });
                }
            }

            int statusCode = (int)HttpStatusCode.OK;
            response = new ModelResponse { Status = statusCode, Message = ReasonPhrases.GetReasonPhrase(statusCode), EuropeanaItems = output };

            // Connector Zenodo to Middleware
            // Build response XML
            var serializer = new XmlSerializer(typeof(ModelResponse));
            using var sw = new StringWriter();
            serializer.Serialize(sw, response);
            string xml = sw.ToString();
            XDocument xdoc = XDocument.Parse(xml);


            // --- Save the finalized XML to Resources before making the HTTP call ---
            _xml.SaveLocalXML(xdoc, Path.Combine(AppContext.BaseDirectory, "Resources"), "ModelResponse");

            return xdoc;
        }
    }
}
