using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System.Xml;

namespace XRCultureHub
{
    public class ViewersRegistry
    {
        public static List<ViewerDescriptor> GetViewerDescriptors(ILogger logger, IConfiguration configuration)
        {
            List<ViewerDescriptor> lsViewerDescriptors = new();
            if (logger == null)
            {
                return lsViewerDescriptors;
            }
            if (configuration == null)
            {
                logger.LogError("Configuration is not set.");
                return lsViewerDescriptors;
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var viewersDir = configuration[$"{FileStorage}:ViewersDir"];
            if (string.IsNullOrEmpty(viewersDir))
            {
                logger.LogError("Viewers path is not configured.");
                return lsViewerDescriptors;
            }

            if (!Directory.Exists(viewersDir))
            {
                logger.LogError($"Viewers directory does not exist: {viewersDir}");
                return lsViewerDescriptors;
            }

            var provider = new PhysicalFileProvider(viewersDir);
            var xmlViewers = provider.GetDirectoryContents("/").Where((fileInfo) =>
            {
                if (fileInfo.IsDirectory)
                    return false;

                if (!fileInfo.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            });

            foreach (var fileInfo in xmlViewers)
            {
                logger.LogInformation($"Found viewer: {fileInfo.Name} at {fileInfo.PhysicalPath}");

                XmlDocument xmlDoc = new();
                xmlDoc.Load(fileInfo.PhysicalPath!);

                lsViewerDescriptors.Add(new()
                {
                    Id = xmlDoc.SelectSingleNode("//Viewer/Id")?.InnerXml ?? "NA",
                    ProviderId = xmlDoc.SelectSingleNode("//Viewer/ProviderId")?.InnerXml ?? "NA",
                    ServiceName = xmlDoc.SelectSingleNode("//Viewer/ServiceName")?.InnerXml ?? "NA",
                    ServiceType = xmlDoc.SelectSingleNode("//Viewer/ServiceType")?.InnerXml ?? "Viewer",                    
                    EndPoint = xmlDoc.SelectSingleNode("//Viewer/EndPoint")?.InnerXml ?? "NA",
                    BackEnd = xmlDoc.SelectSingleNode("//Viewer/BackEnd")?.InnerXml ?? "NA",
                    FrontEnd = xmlDoc.SelectSingleNode("//Viewer/FrontEnd")?.InnerXml ?? "NA",
                    TimeStamp = xmlDoc.SelectSingleNode("//Viewer/TimeStamp")?.InnerXml ?? "NA",
                });
            }

            return lsViewerDescriptors;
        }

        public static bool IsViewerRegistered(ILogger logger, IConfiguration configuration, string endPoint)
        {
            var viewers = GetViewerDescriptors(logger, configuration);
            foreach (var viewer in viewers)
            {
                if (viewer.EndPoint == endPoint)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class ViewerDescriptor
    {
        public string Id { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;        
        public string EndPoint { get; set; } = string.Empty;
        public string BackEnd { get; set; } = string.Empty;
        public string FrontEnd { get; set; } = string.Empty;
        public string TimeStamp { get; set; } = DateTime.Now.ToString();
    }
}
