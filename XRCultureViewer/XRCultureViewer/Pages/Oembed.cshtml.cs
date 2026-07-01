using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Xml;

namespace XRCultureViewer.Pages
{
    /// <summary>
    /// oEmbed endpoint - https://oembed.com/
    /// Supports: GET /oembed?url=...&amp;format=json|xml
    /// </summary>
    [IgnoreAntiforgeryToken]
    public class OembedModel : PageModel
    {
        private readonly ILogger<OembedModel> _logger;
        private readonly IConfiguration _configuration;

        public OembedModel(ILogger<OembedModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>The URL of the model viewer page to embed.</summary>
        [BindProperty(SupportsGet = true)]
        public new string? Url { get; set; }

        /// <summary>Response format: "json" (default) or "xml".</summary>
        [BindProperty(SupportsGet = true)]
        public string? Format { get; set; }

        /// <summary>Optional max width hint from the consumer.</summary>
        [BindProperty(SupportsGet = true)]
        public int? MaxWidth { get; set; }

        /// <summary>Optional max height hint from the consumer.</summary>
        [BindProperty(SupportsGet = true)]
        public int? MaxHeight { get; set; }

        public IActionResult OnGet()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                _logger.LogWarning("oEmbed request missing 'url' parameter.");
                return BadRequest("Missing required parameter: url");
            }

            // Validate that the URL belongs to this service
            var serviceRoot = GetServiceRootUrl();
            if (!Url.StartsWith(serviceRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("oEmbed URL does not match service root: {Url}", Url);
                return NotFound("URL not supported by this provider.");
            }

            // Extract the model query parameter from the URL
            if (!Uri.TryCreate(Url, UriKind.Absolute, out var parsedUri))
            {
                _logger.LogWarning("oEmbed invalid URL: {Url}", Url);
                return BadRequest("Invalid URL format.");
            }

            var query = System.Web.HttpUtility.ParseQueryString(parsedUri.Query);
            var model = query["model"];

            var width = MaxWidth ?? 800;
            var height = MaxHeight ?? 600;

            var providerName = _configuration["Oembed:ProviderName"] ?? "XRCultureViewer";
            var providerUrl = serviceRoot;
            var thumbnailUrl = $"{serviceRoot}Storage?handler=Thumbnail&id={Path.GetFileNameWithoutExtension(model)}.jpg";

            var title = string.IsNullOrWhiteSpace(model)
                ? "3D Model Viewer"
                : $"3D Model: {Path.GetFileNameWithoutExtension(model)}";

            var iframeHtml = $"""
                <iframe src="{Url}" width="{width}" height="{height}" frameborder="0" allowfullscreen></iframe>
                """;

            _logger.LogInformation("oEmbed request for URL: {Url}", Url);

            var format = (Format ?? "json").Trim().ToLowerInvariant();

            if (format == "xml")
            {
                var xml = BuildXmlResponse(title, iframeHtml, width, height, providerName, providerUrl, thumbnailUrl);
                return Content(xml, "text/xml", Encoding.UTF8);
            }

            // JSON
            var json = BuildJsonResponse(title, iframeHtml, width, height, providerName, providerUrl, thumbnailUrl);
            return Content(json, "application/json", Encoding.UTF8);
        }

        private static string BuildJsonResponse(
            string title, string html, int width, int height,
            string providerName, string providerUrl, string thumbnailUrl)
        {
            return $$"""
                {
                  "version": "1.0",
                  "type": "rich",
                  "title": {{System.Text.Json.JsonSerializer.Serialize(title)}},
                  "html": {{System.Text.Json.JsonSerializer.Serialize(html)}},
                  "width": {{width}},
                  "height": {{height}},
                  "thumbnail_url": {{System.Text.Json.JsonSerializer.Serialize(thumbnailUrl)}},
                  "provider_name": {{System.Text.Json.JsonSerializer.Serialize(providerName)}},
                  "provider_url": {{System.Text.Json.JsonSerializer.Serialize(providerUrl)}}
                }
                """;
        }

        private static string BuildXmlResponse(
            string title, string html, int width, int height,
            string providerName, string providerUrl, string thumbnailUrl)
        {
            var sb = new StringBuilder();
            sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
            sb.AppendLine("<oembed>");
            sb.AppendLine("  <version>1.0</version>");
            sb.AppendLine("  <type>rich</type>");
            sb.AppendLine($"  <title>{SecurityElement(title)}</title>");
            sb.AppendLine($"  <html>{SecurityElement(html)}</html>");
            sb.AppendLine($"  <width>{width}</width>");
            sb.AppendLine($"  <height>{height}</height>");
            sb.AppendLine($"  <thumbnail_url>{SecurityElement(thumbnailUrl)}</thumbnail_url>");
            sb.AppendLine($"  <provider_name>{SecurityElement(providerName)}</provider_name>");
            sb.AppendLine($"  <provider_url>{SecurityElement(providerUrl)}</provider_url>");
            sb.AppendLine("</oembed>");
            return sb.ToString();
        }

        // https://learn.microsoft.com/en-us/dotnet/api/system.security.securityelement.escape?view=net-10.0
        private static string SecurityElement(string value) =>
            System.Security.SecurityElement.Escape(value) ?? string.Empty;

        private string GetServiceRootUrl()
        {
            var request = HttpContext.Request;
            return $"{request.Scheme}://{request.Host}{request.PathBase}/";
        }
    }
}