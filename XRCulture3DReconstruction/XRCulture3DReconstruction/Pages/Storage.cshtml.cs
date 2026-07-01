using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;

namespace XRCulture3DReconstruction.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class StorageModel : PageModel
    {
        private readonly ILogger<StorageModel> _logger;
        private readonly IConfiguration _configuration;

        public StorageModel(ILogger<StorageModel> logger, IConfiguration configuration)
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
                var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

                var modelsDir = _configuration[$"{fileStorage}:ModelsDir"];
                if (string.IsNullOrEmpty(modelsDir))
                {
                    _logger.LogError("Models path is not configured");
                    return Content(ViewModelHTTPResponse.ServerError.Replace("%MESSAGE%", "Models path is not configured."), "application/xml");
                }

                _logger.LogInformation($"Using path: {modelsDir}");

                if (string.IsNullOrEmpty(id))
                {
                    _logger.LogError("id is required");
                    return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "id is required."), "application/xml");
                }

                var provider = new PhysicalFileProvider(modelsDir);
                var fileInfo = provider.GetFileInfo(id);

                _logger.LogInformation($"Looking for file: {id}, exists: {fileInfo.Exists}, physical path: {fileInfo.PhysicalPath}");

                if (!fileInfo.Exists || string.IsNullOrEmpty(fileInfo.PhysicalPath))
                {
                    _logger.LogError($"File '{id}' not found at '{fileInfo.PhysicalPath}'");
                    return Content(ViewModelHTTPResponse.NotFound.Replace("%MESSAGE%", $"File '{id}' not found."), "application/xml");
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
                return Content(ViewModelHTTPResponse.ServerError.Replace("%MESSAGE%", $"Server error: {ex.Message}"), "application/xml");
            }
        }

        public IActionResult OnGetLog(string id)
        {
            try
            {
                _logger.LogInformation($"OnGetLog called with id: {id}");

                var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
                var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

                var modelsDir = _configuration[$"{fileStorage}:ModelsDir"];
                if (string.IsNullOrEmpty(modelsDir))
                {
                    _logger.LogError("Models path is not configured");
                    return Content(ViewModelHTTPResponse.ServerError.Replace("%MESSAGE%", "Models path is not configured."), "application/xml");
                }

                _logger.LogInformation($"Using path: {modelsDir}");

                if (string.IsNullOrEmpty(id))
                {
                    _logger.LogError("id is required");
                    return Content(ViewModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "id is required."), "application/xml");
                }

                var provider = new PhysicalFileProvider(modelsDir);
                var fileInfo = provider.GetFileInfo(id);

                _logger.LogInformation($"Looking for file: {id}, exists: {fileInfo.Exists}, physical path: {fileInfo.PhysicalPath}");

                if (!fileInfo.Exists || string.IsNullOrEmpty(fileInfo.PhysicalPath))
                {
                    _logger.LogError($"File '{id}' not found at '{fileInfo.PhysicalPath}'");
                    return Content(ViewModelHTTPResponse.NotFound.Replace("%MESSAGE%", $"File '{id}' not found."), "application/xml");
                }

                _logger.LogInformation($"Returning file: {fileInfo.PhysicalPath}, size: {fileInfo.Length} bytes");
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";
                Response.Headers["Content-Disposition"] = "inline";

                return File(System.IO.File.ReadAllBytes(fileInfo.PhysicalPath), "text/plain; charset=utf-8");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGetLog");
                return Content(ViewModelHTTPResponse.ServerError.Replace("%MESSAGE%", $"Server error: {ex.Message}"), "application/xml");
            }
        }
    }
}
