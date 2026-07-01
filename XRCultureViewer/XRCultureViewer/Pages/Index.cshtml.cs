using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using System.Xml;
using XRCultureViewer.Pages.Shared;

namespace XRCultureViewer.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void OnGet()
        {
        }

        public List<ModelDescriptor> GetModels()
        {
            List<ModelDescriptor> lsModelDescriptors = new();
            if (_configuration == null)
            {
                _logger.LogError("Configuration is not set.");
                return lsModelDescriptors;
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var modelsDir = _configuration[$"{FileStorage}:ModelsDir"];
            if (string.IsNullOrEmpty(modelsDir))
            {
                _logger.LogError("Models path is not configured.");
                return lsModelDescriptors;
            }

            if (!Directory.Exists(modelsDir))
            {
                _logger.LogError($"Models directory does not exist: {modelsDir}");
                return lsModelDescriptors;
            }

            var thumbnailsDir = _configuration[$"{FileStorage}:ThumbnailsDir"];
            if (string.IsNullOrEmpty(thumbnailsDir))
            {
                _logger.LogError("Thumbnails path is not configured.");
                return lsModelDescriptors;
            }

            if (!Directory.Exists(thumbnailsDir))
            {
                _logger.LogError($"Thumbnails directory does not exist: {thumbnailsDir}");
                return lsModelDescriptors;
            }

            var provider = new PhysicalFileProvider(modelsDir);
            var xmlModels = provider.GetDirectoryContents("/").Where((fileInfo) =>
            {
                if (fileInfo.IsDirectory)
                    return false;

                if (!fileInfo.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            });

            foreach (var fileInfo in xmlModels)
            {
                _logger.LogInformation($"Found model: {fileInfo.Name} at {fileInfo.PhysicalPath}");

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(fileInfo.PhysicalPath!);

                var modelId = xmlDoc.SelectSingleNode("//Model/Id")?.InnerText ?? "NA";
                var modelExtension = xmlDoc.SelectSingleNode("//Model/Extension")?.InnerText ?? "NA";
                var thumbnailFileName = $"{Path.Combine(thumbnailsDir!, Path.GetFileNameWithoutExtension(fileInfo.Name))}.jpg";

                var thumbnailUrl = string.Empty;
                if (Path.Exists(thumbnailFileName))
                {
                    thumbnailUrl = $"/Storage?handler=Thumbnail&id={modelId}.jpg";
                }

                lsModelDescriptors.Add(new()
                {
                    Id = modelId,
                    Extension = modelExtension,
                    Name = xmlDoc.SelectSingleNode("//Model/Name")?.InnerText ?? "NA",
                    Description = xmlDoc.SelectSingleNode("//Model/description")?.InnerText ?? "NA",
                    TimeStamp = xmlDoc.SelectSingleNode("//Model/TimeStamp")?.InnerText ?? "NA",
                    ThumbnailUrl = thumbnailUrl
                });
            }

            return lsModelDescriptors = lsModelDescriptors
                .OrderByDescending(m =>
                {
                    if (string.IsNullOrEmpty(m.TimeStamp) || m.TimeStamp == "Unknown")
                        return DateTime.MinValue;
                    return DateTime.TryParse(m.TimeStamp, out var date) ? date : DateTime.MinValue;
                })
                .ToList();
        }
    }

    public class ModelDescriptor
    {
        public string? Id { get; set; }
        public string? Extension { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? TimeStamp { get; set; }
        public string? ThumbnailUrl { get; set; }
    }
}
