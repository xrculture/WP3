using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System.Xml;
using XRCultureViewer.Services;

namespace XRCultureViewer.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class GaussianSplattingViewerModel : PageModel
    {
        private readonly ILogger<ViewerModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IThumbnailService _thumbnailService;
        private readonly IModelLoaderService _modelLoaderService;

        public GaussianSplattingViewerModel(ILogger<ViewerModel> logger, IConfiguration configuration, IThumbnailService thumbnailService, IModelLoaderService modelLoaderService)
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

        private string GetServiceRootUrl()
        {
            var request = HttpContext.Request;
            return $"{request.Scheme}://{request.Host}{request.PathBase}/";
        }
    }
}
