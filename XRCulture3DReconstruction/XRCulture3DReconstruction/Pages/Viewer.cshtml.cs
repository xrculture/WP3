using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Concurrent;
using System.Xml;
using XRCulture3DReconstruction;

namespace XRCulture3DReconstruction.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class ViewerModel : PageModel
    {
        private readonly ILogger<ViewerModel> _logger;
        private readonly IConfiguration _configuration;

        public ViewerModel(ILogger<ViewerModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public string? Model { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ShowSettingsPanel { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ShowFaces { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ShowWireframes { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ShowLines { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ShowPoints { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ShowModelCS { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ShowWorldCS { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? ShowNavigator { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BackgroundColor { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectionColor { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Rotation { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? EyeVector { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? TargetVector { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? UpVector { get; set; }

        public void OnGet()
        {
            // Default model if none is provided #todo: set default model
            //if (string.IsNullOrEmpty(Model))
            //{
            //    Model = "f7aa9163-2d18-404c-a2ef-65693a5960d6.binz";
            //}
        }

        /*
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>
        <ViewModelRequest>
            <Name>%NAME%</Name>
                <Parameters>%PARAMETERS%</Parameters>
        </ViewModelRequest>";
        */
        public async Task<IActionResult> OnPostViewModelAsync()
        {
            if (!Request.HasFormContentType)
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Content-Type must be multipart/form-data."));

            var form = await Request.ReadFormAsync();

            // Get XML request part
            var xmlRequest = form.Files.FirstOrDefault(f => f.Name == "request");
            string? xmlString = null;
            if (xmlRequest != null)
            {
                using var reader = new StreamReader(xmlRequest.OpenReadStream());
                xmlString = await reader.ReadToEndAsync();
            }
            else if (form.TryGetValue("request", out var xmlField))
            {
                xmlString = xmlField.ToString();
            }
            if (string.IsNullOrEmpty(xmlString))
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Missing XML request part."));

            // Validate XML
            if (!xmlString.Trim().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Invalid XML format."));
            if (!xmlString.Contains("<ViewModelRequest", StringComparison.OrdinalIgnoreCase))
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "XML does not contain <ViewModelRequest> element."));
            if (!xmlString.Contains("<Name", StringComparison.OrdinalIgnoreCase))
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "XML does not contain <Name> element."));
            if (!xmlString.Contains("<Parameters", StringComparison.OrdinalIgnoreCase))
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "XML does not contain <Parameters> element."));

            // Validate XML structure
            if (xmlString.Length > 1000000) // 1 MB limit
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "XML request is too large. Maximum size is 1 MB."));

            XmlDocument viewModelRequestXml = new();
            try
            {
                viewModelRequestXml.LoadXml(xmlString);
                var root = viewModelRequestXml.DocumentElement;
                if (root == null || root.Name != "ViewModelRequest")
                    return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "XML root element must be <ViewModelRequest>."));
                if (root.SelectSingleNode("Name") == null)
                    return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "XML must contain <Name> element."));
                if (root.SelectSingleNode("Parameters") == null)
                    return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "XML must contain <Parameters> element."));
            }
            catch (XmlException ex)
            {
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", $"Invalid XML format: {ex.Message}"));
            }

            var model = viewModelRequestXml.SelectSingleNode("//ViewModelRequest/Name")?.InnerText;
            if (string.IsNullOrEmpty(model))
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Bad request: 'Name'."));

            // Get zip file part
            var zipFile = form.Files.FirstOrDefault(f => f.Name == "file");
            if (zipFile == null || zipFile.Length == 0)
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Missing or empty zip file."));

            if (zipFile.ContentType != "application/zip")
                return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Invalid file type. Expected application/zip."));

            var viewerPath = _configuration["Paths:Viewer"];
            if (string.IsNullOrEmpty(viewerPath))
            {
                return Content(ViewModelHTTPResponse.ServerError.Replace("%MESSAGE%", "Viewer path is not configured."), "application/xml");
            }

            var dataDir = Path.Combine(viewerPath, @"data");

            // Save zip
            var resultId = Guid.NewGuid().ToString();
            var tempZipPath = Path.Combine(dataDir, $"{resultId}{Path.GetExtension(zipFile.FileName)}");
            using (var fs = System.IO.File.Create(tempZipPath))
            using (var zipStream = zipFile.OpenReadStream())
            {
                await zipStream.CopyToAsync(fs);
            }

            //# todo:check file extension and extract if needed
            //var extractDir = Path.Combine(Path.GetTempPath(), resultId);
            //Directory.CreateDirectory(extractDir);
            //System.IO.Compression.ZipFile.ExtractToDirectory(tempZipPath, extractDir);
            //System.IO.File.Delete(tempZipPath);

            var serviceUrl = GetServiceRootUrl();
            var response = ViewModelHTTPResponse.SuccessWithParameters.Replace("%PARAMETERS%",
                $"<ResultId>{resultId}</ResultId><URL>{serviceUrl}Viewer?model={resultId}{Path.GetExtension(zipFile.FileName)}</URL>");

            return Content(response, "application/xml");
        }

        private string GetServiceRootUrl()
        {
            var request = HttpContext.Request;
            return $"{request.Scheme}://{request.Host}{request.PathBase}/";
        }
    }
}
