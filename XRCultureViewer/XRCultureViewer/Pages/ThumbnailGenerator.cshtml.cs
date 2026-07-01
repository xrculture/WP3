using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System.Xml;
using XRCultureViewer.Services;

namespace XRCultureViewer.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ThumbnailGeneratorModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ThumbnailGeneratorModel> _logger;
    private readonly IThumbnailService _thumbnailService;
    private readonly IModelLoaderService _modelLoaderService;

    public ThumbnailGeneratorModel(IConfiguration configuration, ILogger<ThumbnailGeneratorModel> logger, IThumbnailService thumbnailService, IModelLoaderService modelLoaderService)
    {
        _configuration = configuration;
        _logger = logger;
        _thumbnailService = thumbnailService;
        _modelLoaderService = modelLoaderService;
    }

    [BindProperty(SupportsGet = true)]
    public string? ModelId { get; set; }

    public IActionResult OnGet()
    {
        if (string.IsNullOrEmpty(ModelId))
            return BadRequest("modelId is required.");

        return Page();
    }

    /// <summary>
    /// GET /ThumbnailGenerator?handler=Status&amp;modelId=abc
    /// Returns 200 + JSON when thumbnail exists, 202 (Accepted) while still pending.
    /// </summary>
    public IActionResult OnGetStatus()
    {
        if (string.IsNullOrEmpty(ModelId))
            return BadRequest();

        var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
        var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

        var thumbnailsDir = _configuration[$"{FileStorage}:ThumbnailsDir"];
        if (string.IsNullOrEmpty(thumbnailsDir))
        {
            _logger.LogError("Thumbnails path is not configured");
            return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Thumbnails path is not configured."), "application/xml");
        }

        var fileName = Path.GetFileNameWithoutExtension(ModelId) + ".jpg";
        if (System.IO.File.Exists(Path.Combine(thumbnailsDir, fileName)))
        {
            return new JsonResult(new { ready = true, url = $"/thumbnails/{fileName}" });
        }

        return StatusCode(StatusCodes.Status202Accepted, new { ready = false });
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

        _logger.LogInformation($"****** Request ******\n{body}");

        var serviceUrl = GetServiceRootUrl();

        if (bJSONContentType)
        {
            return await _modelLoaderService.LoadModelJSONAsync(jsonRequest, serviceUrl);
        } // application/json
        else
        {
            XmlDocument generateThumbnailRequestXml = new();
            try
            {
                var xmlBody = JsonConvert.DeserializeObject<string>(body);
                if (string.IsNullOrEmpty(xmlBody))
                {
                    _logger.LogInformation("Received empty XML request.");
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Received empty XML request."));
                }

                generateThumbnailRequestXml.LoadXml(xmlBody);
            }
            catch (XmlException ex)
            {
                _logger.LogError(ex, $"Invalid XML format: {ex.Message}");
                return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", $"Invalid XML format: {ex.Message}"));
            }

            return await _modelLoaderService.LoadModelXMLAsync(generateThumbnailRequestXml, serviceUrl);
        } // application/xml
    }

    private string GetServiceRootUrl()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}/";
    }

    public string GetThumbnailIFrameUrl()
    {
        var fileExtension = Path.GetExtension(ModelId ?? string.Empty).ToLowerInvariant();
        var viewer = fileExtension.Equals(".splat", StringComparison.OrdinalIgnoreCase) ||
            fileExtension.Equals(".ply", StringComparison.OrdinalIgnoreCase) ?
                "gaussiansplattingviewer" : "viewer";

        return $"/viewer/{viewer}.html?model={Uri.EscapeDataString(ModelId!)}&thumbnail=1";
    }
}