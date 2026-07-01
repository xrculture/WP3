using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System.Text;
using System.Xml;
using XRCultureViewer.Services;

namespace XRCultureViewer.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class ViewerModel : PageModel
    {
        private readonly ILogger<ViewerModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IThumbnailService _thumbnailService;
        private readonly IModelLoaderService _modelLoaderService;

        public ViewerModel(ILogger<ViewerModel> logger, IConfiguration configuration, IThumbnailService thumbnailService, IModelLoaderService modelLoaderService)
        {
            _logger = logger;
            _configuration = configuration;
            _thumbnailService = thumbnailService;
            _modelLoaderService = modelLoaderService;
        }

        [BindProperty(SupportsGet = true)]
        public string? Model { get; set; }

        public void OnGet()
        {
            // Default model if none is provided #todo: set default model
            //if (string.IsNullOrEmpty(Model))
            //{
            //    Model = "f7aa9163-2d18-404c-a2ef-65693a5960d6.binz";
            //}
        }

        /*
        <?xml version=""1.0"" encoding=""UTF-8""?>
        <ModelLoadingRequest>
            <Source>
              <LocalSource>
                <Name>%NAME%</Name>
                <Description>3D Model file</Description>
                <FileContent dimension=""%SIZE%"" extension=""%EXTENSION%"">%BASE64_CONTENT%</FileContent>
              </LocalSource>
            </Source>
        </ModelLoadingRequest>
        */
        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Received request.");

            bool bJSONContentType = Request.ContentType?.StartsWith("application/json") == true;

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrEmpty(body))
            {
                _logger.LogInformation("Received empty request.");
                return Content(bJSONContentType ?
                    HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Received empty request.") :
                    HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Received empty request."));
            }

            dynamic? jsonRequest = JsonConvert.DeserializeObject(body);
            if (jsonRequest == null)
            {
                _logger.LogInformation("Received empty request.");
                return Content(bJSONContentType ?
                    HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Received empty request.") :
                    HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Received empty request."));
            }

            _logger.LogInformation($"****** Request ******\n{(body.Length > 500 ? body.Substring(0, 500) + "..." : body)}");

            var serviceUrl = GetServiceRootUrl();

            if (bJSONContentType)
            {
                return await _modelLoaderService.LoadModelJSONAsync(jsonRequest, serviceUrl);
            } // application/json
            else
            {
                XmlDocument viewModelRequestXml = new();
                try
                {
                    var xmlBody = JsonConvert.DeserializeObject<string>(body);
                    if (string.IsNullOrEmpty(xmlBody))
                    {
                        _logger.LogInformation("Received empty XML request.");
                        return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Received empty XML request."));
                    }

                    viewModelRequestXml.LoadXml(xmlBody);
                }
                catch (XmlException ex)
                {
                    _logger.LogError(ex, $"Invalid XML format: {ex.Message}");
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", $"Invalid XML format: {ex.Message}"));
                }

                return await _modelLoaderService.LoadModelXMLAsync(viewModelRequestXml, serviceUrl);
            } // application/xml
        }

        /*
         * multipart/form-data
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>
        <ViewModelRequest>
            <Name>%NAME%</Name>
                <Parameters>%PARAMETERS%</Parameters>
        </ViewModelRequest>";
        */
        public async Task<IActionResult> OnPostViewModelAsync()
        {
            _logger.BeginScope("ViewerModel.OnPostViewModelAsync");
            _logger.LogInformation("Processing model upload request.");

            // Check if the request is multipart/form-data
            if (!Request.HasFormContentType)
            {
                _logger.LogWarning("Invalid request content type: {ContentType}", Request.ContentType);
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Content-Type must be multipart/form-data."));
            }

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
            {
                _logger.LogWarning("Missing XML request part.");
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Missing XML request part."));
            }

            // Validate XML
            if (!xmlString.Trim().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Invalid XML format."));
            if (!xmlString.Contains("<ViewModelRequest", StringComparison.OrdinalIgnoreCase))
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "XML does not contain <ViewModelRequest> element."));
            if (!xmlString.Contains("<Name", StringComparison.OrdinalIgnoreCase))
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "XML does not contain <Name> element."));
            if (!xmlString.Contains("<Parameters", StringComparison.OrdinalIgnoreCase))
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "XML does not contain <Parameters> element."));
            if (xmlString.Length > 1000000) // 1 MB limit
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "XML request is too large. Maximum size is 1 MB."));

            XmlDocument viewModelRequestXml = new();
            try
            {
                viewModelRequestXml.LoadXml(xmlString);
                var root = viewModelRequestXml.DocumentElement;
                if (root == null || root.Name != "ViewModelRequest")
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "XML root element must be <ViewModelRequest>."));
                if (root.SelectSingleNode("Name") == null)
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "XML must contain <Name> element."));
                if (root.SelectSingleNode("Parameters") == null)
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "XML must contain <Parameters> element."));
            }
            catch (XmlException ex)
            {
                _logger.LogError(ex, "Invalid XML format.");
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", $"Invalid XML format: {ex.Message}"));
            }

            var model = viewModelRequestXml.SelectSingleNode("//ViewModelRequest/Name")?.InnerText;
            if (string.IsNullOrEmpty(model))
            {
                _logger.LogError("Bad request: 'Name'.");
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Bad request: 'Name'."));
            }

            // Get zip file part
            var zipFile = form.Files.FirstOrDefault(f => f.Name == "file");
            if (zipFile == null || zipFile.Length == 0)
            {
                _logger.LogWarning("Missing or empty zip file.");
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Missing or empty zip file."));
            }

            if (zipFile.ContentType != "application/zip")
            {
                _logger.LogWarning("Invalid file type: {ContentType}. Expected application/zip.", zipFile.ContentType);
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Invalid file type. Expected application/zip."));
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var modelsDir = _configuration[$"{FileStorage}:ModelsDir"];
            if (string.IsNullOrEmpty(modelsDir))
            {
                _logger.LogError("Models path is not configured.");
                return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Models path is not configured."));
            }

            // Save zip
            var resultId = Guid.NewGuid().ToString();
            var tempZipPath = Path.Combine(modelsDir, $"{resultId}{Path.GetExtension(zipFile.FileName)}");
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

            StringBuilder xml = new();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<Model>");
            xml.AppendLine($"\t<Id>{resultId}</Id>");
            xml.AppendLine($"\t<Extension>{Path.GetExtension(zipFile.FileName)}</Extension>");
            xml.AppendLine($"\t<Name>{model}</Name>");
            xml.AppendLine($"\t<Description>{model}</Description>"); //#todo: set description
            xml.AppendLine($"\t<TimeStamp>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</TimeStamp>");
            xml.AppendLine("</Model>");
            System.IO.File.WriteAllText(Path.Combine(modelsDir, $"{resultId}.xml"), xml.ToString());

            var serviceUrl = GetServiceRootUrl();
            var response = HTTPResponse.SuccessWithParametersXML.Replace("%PARAMETERS%",
                $"<ResultId>{resultId}</ResultId><URL>{serviceUrl}Viewer?model={resultId}{Path.GetExtension(zipFile.FileName)}</URL>");
            _logger.LogInformation("Model uploaded successfully with ID: {ResultId}", resultId);
            return Content(response, "application/xml");
        }

        private string GetServiceRootUrl()
        {
            var request = HttpContext.Request;
            return $"{request.Scheme}://{request.Host}{request.PathBase}/";
        }
    }
}