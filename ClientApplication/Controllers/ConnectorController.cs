using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Europeana3D.Web.Models;
using Europeana3D.Web.Services;
using Europeana3D.Web.Services.Upload;
using System.Xml.Linq;
using System.Net.Mime;
using System.Xml.Serialization;

namespace Europeana3D.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConnectorController : ControllerBase
    {
        private readonly ViewerService _viewers;
        private readonly BridgeService _bridge;
        private readonly S3Service _s3;
        private readonly ZenodoService _zenodo;
        private readonly ZenodoUploadService _zenodoUpload;
        private readonly S3UploadService _s3Upload;
        private readonly IHttpClientFactory _http;

        public ConnectorController(ViewerService viewers, BridgeService bridge, S3Service s3, ZenodoService zenodo,
            ZenodoUploadService zenodoUpload, S3UploadService s3Upload, IHttpClientFactory http)
        {
            _viewers = viewers;
            _bridge = bridge;
            _s3 = s3;
            _zenodo = zenodo;
            _zenodoUpload = zenodoUpload;
            _s3Upload = s3Upload;
            _http = http;
        }

        [HttpPost("search-europeana")]
        [Consumes(MediaTypeNames.Text.Plain)]
        public async Task<IActionResult> SearchEuropeana([FromBody] string? xmlText)
        {
            XDocument doc = XDocument.Parse(xmlText!);
            var xdoc = await _bridge.ProcessAsync(doc);
            return Ok(xdoc.ToString());
        }

        [HttpPost("search-zenodo")]
        [Consumes(MediaTypeNames.Text.Plain)]
        public async Task<IActionResult> SearchZenodo([FromBody] string? xmlText)
        {
            XDocument doc = XDocument.Parse(xmlText!);
            var xdoc = await _zenodo.ProcessAsync(doc);
            return Ok(xdoc.ToString());
        }

        [HttpPost("search-amazon")]
        [Consumes(MediaTypeNames.Text.Plain)]
        public async Task<IActionResult> SearchS3([FromBody] string? xmlText)
        {
            XDocument doc = XDocument.Parse(xmlText!);
            var xdoc = await _s3.ProcessAsync(doc);
            return Ok(xdoc.ToString());
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] string? xmlRequest, IFormFile? file)
        {
            if (string.IsNullOrWhiteSpace(xmlRequest))
                return BadRequest("Missing 'xmlRequest' form field.");

            XDocument doc;
            try { doc = XDocument.Parse(xmlRequest); }
            catch (Exception ex) { return BadRequest($"Invalid XML: {ex.Message}"); }

            var root = doc.Root!;
            var serviceId = ((string?)root.Element("TargetRepository")?.Element("ServiceID") ?? string.Empty).ToLowerInvariant();

            var model = new UploadViewModel
            {
                Title = (string?)root.Element("Metadata")?.Element("Title") ?? string.Empty,
                Description = (string?)root.Element("Metadata")?.Element("Description"),
                Creator = (string?)root.Element("Metadata")?.Element("Creator"),
                Rights = (string?)root.Element("Metadata")?.Element("Rights"),
                License = (string?)root.Element("Metadata")?.Element("License"),
                PublicationYear = (string?)root.Element("Metadata")?.Element("PublicationYear"),
                Tags = string.Join(",", root.Element("Metadata")?.Element("Tags")?.Elements("Tag").Select(t => t.Value) ?? Enumerable.Empty<string>()),
                AccessToken = (string?)root.Element("TargetRepository")?.Element("AccessToken"),
                BucketName = (string?)root.Element("TargetRepository")?.Element("BucketName"),
                FolderPath = (string?)root.Element("TargetRepository")?.Element("FolderPath"),
                SourceUrl = (string?)root.Element("Source")?.Element("UrlSource")?.Element("Url"),
                PublishImmediately = (string?)root.Element("UploadOptions")?.Element("PublishImmediately")?.Attribute("value") == "True",
                TargetRepositoryId = serviceId == "zenodo" ? "Zenodo" : "AWS S3"
            };

            IUploadService? service = serviceId switch
            {
                "zenodo" => _zenodoUpload,
                "s3" => _s3Upload,
                _ => null
            };

            if (service == null)
                return BadRequest($"Unknown ServiceID '{serviceId}'. Supported: zenodo, s3.");

            Stream? stream = null;
            string filename = string.Empty;
            long fileSize = 0;

            try
            {
                if (file != null)
                {
                    stream = file.OpenReadStream();
                    filename = Path.GetFileName(file.FileName);
                    fileSize = file.Length;
                }
                else if (!string.IsNullOrWhiteSpace(model.SourceUrl))
                {
                    var client = _http.CreateClient();
                    var resp = await client.GetAsync(model.SourceUrl);
                    if (!resp.IsSuccessStatusCode)
                        return BadRequest($"Could not fetch source URL: {resp.StatusCode}");
                    var ms = new MemoryStream();
                    await resp.Content.CopyToAsync(ms);
                    ms.Position = 0;
                    stream = ms;
                    filename = Path.GetFileName(new Uri(model.SourceUrl).LocalPath);
                    fileSize = ms.Length;
                }
                else
                {
                    return BadRequest("Provide a file part or a UrlSource in the XML.");
                }

                var response = await service.UploadAsync(model, stream, filename, fileSize);

                var serializer = new XmlSerializer(typeof(ModelUploadResponse));
                var sb = new System.Text.StringBuilder();
                using (var writer = new System.IO.StringWriter(sb))
                    serializer.Serialize(writer, response);

                return Ok(sb.ToString());
            }
            finally
            {
                stream?.Dispose();
            }
        }
    }
}
