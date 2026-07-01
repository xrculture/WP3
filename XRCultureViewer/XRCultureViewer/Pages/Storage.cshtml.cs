using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;

namespace XRCultureViewer.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class StorageModel : PageModel
    {
        private readonly ILogger<ViewerModel> _logger;
        private readonly IConfiguration _configuration;

        public StorageModel(ILogger<ViewerModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }
        public void OnGet()
        {
        }

        public IActionResult OnGetModel(string id)
        {
            try
            {
                _logger.LogInformation($"OnGetModel called with id: {id}");

                var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
                var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

                var modelsDir = _configuration[$"{FileStorage}:ModelsDir"];
                if (string.IsNullOrEmpty(modelsDir))
                {
                    _logger.LogError("Models path is not configured");
                    return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Models path is not configured."), "application/xml");
                }

                _logger.LogInformation($"Using viewer path: {modelsDir}");

                if (string.IsNullOrEmpty(id))
                {
                    _logger.LogError("id is required");
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "id is required."), "application/xml");
                }

                var provider = new PhysicalFileProvider(modelsDir);
                var fileInfo = provider.GetFileInfo(id);

                _logger.LogInformation($"Looking for file: {id}, exists: {fileInfo.Exists}, physical path: {fileInfo.PhysicalPath}");

                if (!fileInfo.Exists || string.IsNullOrEmpty(fileInfo.PhysicalPath))
                {
                    _logger.LogError($"File '{id}' not found at '{fileInfo.PhysicalPath}'");
                    return Content(HTTPResponse.NotFoundXML.Replace("%MESSAGE%", $"File '{id}' not found."), "application/xml");
                }

                _logger.LogInformation($"Returning file: {fileInfo.PhysicalPath}, size: {fileInfo.Length} bytes");
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                return File(System.IO.File.ReadAllBytes(fileInfo.PhysicalPath), "application/octet-stream", Path.GetFileName(id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGetModel");
                return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", $"Server error: {ex.Message}"), "application/xml");
            }
        }

        public IActionResult OnGetThumbnail(string id)
        {
            try
            {
                _logger.LogInformation($"OnGetThumbnail called with id: {id}");

                var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
                var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

                var thumbnailsDir = _configuration[$"{FileStorage}:ThumbnailsDir"];
                if (string.IsNullOrEmpty(thumbnailsDir))
                {
                    _logger.LogError("Thumbnails path is not configured");
                    return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Thumbnails path is not configured."), "application/xml");
                }

                _logger.LogInformation($"Using thumbnails path: {thumbnailsDir}");

                if (string.IsNullOrEmpty(id))
                {
                    _logger.LogError("id is required");
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "id is required."), "application/xml");
                }

                var provider = new PhysicalFileProvider(thumbnailsDir);
                var fileInfo = provider.GetFileInfo(id);

                _logger.LogInformation($"Looking for file: {id}, exists: {fileInfo.Exists}, physical path: {fileInfo.PhysicalPath}");

                if (!fileInfo.Exists || string.IsNullOrEmpty(fileInfo.PhysicalPath))
                {
                    _logger.LogError($"File '{id}' not found at '{fileInfo.PhysicalPath}'");
                    return Content(HTTPResponse.NotFoundXML.Replace("%MESSAGE%", $"File '{id}' not found."), "application/xml");
                }

                _logger.LogInformation($"Returning file: {fileInfo.PhysicalPath}, size: {fileInfo.Length} bytes");
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                return File(System.IO.File.ReadAllBytes(fileInfo.PhysicalPath), "application/octet-stream", Path.GetFileName(id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGetThumbnail");
                return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", $"Server error: {ex.Message}"), "application/xml");
            }
        }

        #region Thumbnails
        // POST /Storage?handler=Thumbnail
        // Example: generateAndUploadThumbnail() in viewer.js
        public async Task<IActionResult> OnPostThumbnailAsync(
            [FromBody] ThumbnailRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(request.ModelId) || string.IsNullOrEmpty(request.ImageBase64))
            {
                return BadRequest();
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var thumbnailsDir = _configuration[$"{FileStorage}:ThumbnailsDir"];
            if (string.IsNullOrEmpty(thumbnailsDir))
            {
                _logger.LogError("Thumbnails path is not configured");
                return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Thumbnails path is not configured."), "application/xml");
            }

            var bytes = Convert.FromBase64String(request.ImageBase64);

            var fileName = Path.GetFileNameWithoutExtension(request.ModelId) + ".jpg";
            await System.IO.File.WriteAllBytesAsync(Path.Combine(thumbnailsDir, fileName), bytes, ct);

            return new OkResult();
        }

        public record ThumbnailRequest(string ModelId, string ImageBase64);
        #endregion // Thumbnails
    }
}
