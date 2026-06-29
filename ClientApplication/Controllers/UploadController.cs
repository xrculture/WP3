using Europeana3D.Web.Models;
using Europeana3D.Web.Services;
using Europeana3D.Web.Services.Upload;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Europeana3D.Web.Controllers
{
    public class UploadController : Controller
    {
        private readonly RepositoryService _repositories;
        private readonly ZenodoUploadService _zenodoUpload;
        private readonly S3UploadService _s3Upload;
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _configuration;

        public UploadController(
            RepositoryService repositories,
            ZenodoUploadService zenodoUpload,
            S3UploadService s3Upload,
            IHttpClientFactory http,
            IConfiguration configuration)
        {
            _repositories = repositories;
            _zenodoUpload = zenodoUpload;
            _s3Upload = s3Upload;
            _http = http;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            PrepareViewData();
            return View(new UploadViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Submit(UploadViewModel model)
        {
            if (model.LocalFile == null && string.IsNullOrWhiteSpace(model.SourceUrl))
                ModelState.AddModelError(string.Empty, "Provide a local file or a source URL.");

            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Title is required.");

            if (string.IsNullOrWhiteSpace(model.TargetRepositoryId))
                ModelState.AddModelError(nameof(model.TargetRepositoryId), "Select a repository.");

            if (!ModelState.IsValid)
            {
                PrepareViewData();
                return View("Index", model);
            }

            var supportedPattern = _configuration["Options:SupportedFormats"] ?? "obj|ifc|dae|glb";
            Stream? fileStream = null;
            string filename = string.Empty;
            long fileSize = 0;

            try
            {
                if (model.LocalFile != null)
                {
                    var ext = Path.GetExtension(model.LocalFile.FileName).TrimStart('.');
                    if (!Regex.IsMatch(ext, $"^({supportedPattern})$", RegexOptions.IgnoreCase))
                    {
                        ModelState.AddModelError(nameof(model.LocalFile), $"Unsupported format. Allowed: {supportedPattern}");
                        PrepareViewData();
                        return View("Index", model);
                    }
                    fileStream = model.LocalFile.OpenReadStream();
                    filename = Path.GetFileName(model.LocalFile.FileName);
                    fileSize = model.LocalFile.Length;
                }
                else
                {
                    var client = _http.CreateClient();
                    var resp = await client.GetAsync(model.SourceUrl);
                    if (!resp.IsSuccessStatusCode)
                    {
                        ModelState.AddModelError(nameof(model.SourceUrl), "Could not fetch the source URL.");
                        PrepareViewData();
                        return View("Index", model);
                    }
                    var ms = new MemoryStream();
                    await resp.Content.CopyToAsync(ms);
                    ms.Position = 0;
                    fileStream = ms;
                    filename = Path.GetFileName(new Uri(model.SourceUrl!).LocalPath);
                    fileSize = ms.Length;
                }

                IUploadService service = model.TargetRepositoryId switch
                {
                    "Zenodo" => _zenodoUpload,
                    "AWS S3" => _s3Upload,
                    _ => throw new InvalidOperationException($"Unknown repository: {model.TargetRepositoryId}")
                };

                var response = await service.UploadAsync(model, fileStream, filename, fileSize);
                return View("UploadResult", response);
            }
            catch (Exception ex)
            {
                return View("UploadResult", new ModelUploadResponse
                {
                    Status = 500,
                    Message = "Unexpected error.",
                    Errors = new() { new UploadError { Code = "EXCEPTION", Detail = ex.Message } }
                });
            }
            finally
            {
                fileStream?.Dispose();
            }
        }

        private void PrepareViewData()
        {
            var repos = _repositories.GetAll();
            ViewData["repositories"] = repos;
            ViewData["reposJson"] = System.Text.Json.JsonSerializer.Serialize(repos);
            ViewData["supportedFormats"] = _configuration["Options:SupportedFormats"] ?? "obj|ifc|dae|glb";
        }
    }
}
