using Amazon.S3;                       // [ADDED for S3 integration]
using Amazon.S3.Model;                 // [ADDED for S3 integration]
using Europeana3D.Web.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using System.Xml.Serialization;
using System.Xml.Linq;

namespace Europeana3D.Web.Services
{
    // [ADDED for S3 integration]
    public class S3Service
    {
        private readonly IAmazonS3 _s3;
        private readonly IConfiguration _configuration;
        private readonly EuropeanaService _europeana;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly XmlTemplateService _xml;

        public S3Service(IAmazonS3 s3, IHttpClientFactory httpClientFactory, EuropeanaService europeana, IConfiguration configuration, XmlTemplateService xml) // [ADDED for S3 integration]
        {
            _s3 = s3;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _europeana = europeana;
            _xml = xml;
        }

        /// <summary>
        /// Search inside a given bucket for 3D model candidates matching the query.
        /// Returns a list of EuropeanaItem so UI can stay unchanged.
        /// </summary>
        public async Task<List<EuropeanaItem>> Search3DInBucketAsync( // [ADDED for S3 integration]
            string bucketName,
            string searchQuery,
            CancellationToken ct = default)
        {

            // UI to Middleware for SearchRequest
            var tmpl = _xml.LoadModelRequestTemplate();
            var root = tmpl.Root!;

            var filters = root.Element("Filters")!
            .Element("Filter")!;
            filters.Element("SearchQuery")!.Value = searchQuery;

            string region = _configuration["AWS:Region"];
            root.Element("ServiceUrl")!.Value = $"https://{bucketName}.s3.{region}.amazonaws.com";

            // --- Save the finalized XML to Resources before making the HTTP call ---
            _xml.SaveLocalXML(tmpl, Path.Combine(AppContext.BaseDirectory, "Resources"), "ModelRequest");

            // Send request to S3
            var xml = await ProcessAsync(tmpl);

            // Middleware to UI for SearchResponse
            var serializer = new XmlSerializer(typeof(ModelResponse));
            using var sr = new StringReader(xml.ToString());
            var items = (ModelResponse)serializer.Deserialize(sr)!;

            return items.EuropeanaItems;
        }

        public async Task<XDocument> ProcessAsync(XDocument requestXml)
        {
            // Connector Middleware to S3 for SearchRequest

            var modelReq = requestXml.Root!;

            var serviceUrl = (string?)modelReq.Element("ServiceUrl") ?? throw new InvalidOperationException("ServiceUrl not found");
            var pagination = modelReq.Element("Pagination");
            var lengthAttr = pagination?.Attribute("length")?.Value;
            var rows = int.TryParse(lengthAttr, out var len) ? len : 0;


            var filters = modelReq.Element("Filters")?.Elements("Filter").FirstOrDefault();
            var searchQuery = filters?.Element("SearchQuery")?.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(searchQuery))
                throw new InvalidOperationException("Search Query not found");

            var results = new List<EuropeanaItem>();

            Uri uri = new Uri(serviceUrl);
            string host = uri.Host;
            string bucketName = host.Split(".").First();
            // We reuse SupportedFormats from config
            var supportedPattern = _configuration["Options:SupportedFormats"];
            var regexExt = new Regex(@"\.(" + supportedPattern + ")$", RegexOptions.IgnoreCase);

            // We will paginate through S3 objects via ListObjectsV2
            string? continuationToken = null;

            int statusCode = (int)HttpStatusCode.OK;

            do
            {
                var req = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    ContinuationToken = continuationToken
                };

                var resp = await _s3.ListObjectsV2Async(req);
                statusCode = (int)resp.HttpStatusCode;

                foreach (var obj in resp.S3Objects)
                {
                    // very simple match rule:
                    // key contains searchQuery (case-insensitive)
                    // and extension is supported
                    if (!obj.Key.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!regexExt.IsMatch(obj.Key))
                        continue;

                    // Build a direct URL for download/view.
                    CancellationToken ct = default;
                    var fileUrl = $"{serviceUrl}/{obj.Key}"; // [ADDED for S3 integration]
                    var fc = await _europeana.ValidateAsync(fileUrl, ct);
                    var fileCandidate = new FileCandidate();
                    if (fc != null)
                    {
                        fileCandidate = fc;

                        var europeanaItem = new EuropeanaItem
                        {
                            Id = obj.Key,
                            Title = obj.Key,
                            Previews = new List<string> { "images/3dmodel.png" },
                            Supported3DFiles = new List<FileCandidate> { fileCandidate }
                        };

                        results.Add(europeanaItem);
                    }
                }

                continuationToken = (resp.IsTruncated ?? false) ? resp.NextContinuationToken : null;

            } while (!string.IsNullOrEmpty(continuationToken));

            var response = new ModelResponse { Status = statusCode, Message = ReasonPhrases.GetReasonPhrase(statusCode), EuropeanaItems = results };

            // Connector S3 to Middleware
            // Build response XML
            var serializer = new XmlSerializer(typeof(ModelResponse));
            using var sw = new StringWriter();
            serializer.Serialize(sw, response);
            string xml = sw.ToString();
            XDocument doc = XDocument.Parse(xml);


            // --- Save the finalized XML to Resources before making the HTTP call ---
            _xml.SaveLocalXML(doc, Path.Combine(AppContext.BaseDirectory, "Resources"), "ModelResponse");

            return doc;
        }
    }
}
