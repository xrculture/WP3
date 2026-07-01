using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System.Xml;

namespace XRCultureHub
{
    public class ServicesRegistry
    {
        public static List<ServiceDescriptor> GetServiceDescriptors(ILogger logger, IConfiguration configuration)
        {
            List<ServiceDescriptor> lsServiceDescriptors = new();
            if (logger == null)
            {
                return lsServiceDescriptors;
            }
            if (configuration == null)
            {
                logger.LogError("Configuration is not set.");
                return lsServiceDescriptors;
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var servicesDir = configuration[$"{FileStorage}:ServicesDir"];
            if (string.IsNullOrEmpty(servicesDir))
            {
                logger.LogError("Services path is not configured.");
                return lsServiceDescriptors;
            }

            if (!Directory.Exists(servicesDir))
            {
                logger.LogError($"Services directory does not exist: {servicesDir}");
                return lsServiceDescriptors;
            }

            var provider = new PhysicalFileProvider(servicesDir);
            var xmlServices = provider.GetDirectoryContents("/").Where((fileInfo) =>
            {
                if (fileInfo.IsDirectory)
                    return false;

                if (!fileInfo.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            });

            foreach (var fileInfo in xmlServices)
            {
                logger.LogInformation($"Found service: {fileInfo.Name} at {fileInfo.PhysicalPath}");

                XmlDocument xmlDoc = new();
                xmlDoc.Load(fileInfo.PhysicalPath!);

                lsServiceDescriptors.Add(new()
                {
                    Id = xmlDoc.SelectSingleNode("//Service/Id")?.InnerXml ?? "NA",
                    ProviderId = xmlDoc.SelectSingleNode("//Service/ProviderId")?.InnerXml ?? "NA",
                    ServiceName = xmlDoc.SelectSingleNode("//Service/ServiceName")?.InnerXml ?? "NA",
                    ServiceType = xmlDoc.SelectSingleNode("//Service/ServiceType")?.InnerXml ?? "NA",                    
                    EndPoint = xmlDoc.SelectSingleNode("//Service/EndPoint")?.InnerXml ?? "NA",
                    UploadEndPoint = xmlDoc.SelectSingleNode("//Service/UploadEndPoint")?.InnerXml ?? "NA",
                    BackEnd = xmlDoc.SelectSingleNode("//Service/BackEnd")?.InnerXml ?? "NA",
                    TimeStamp = xmlDoc.SelectSingleNode("//Service/TimeStamp")?.InnerXml ?? "NA",
                });
            }

            return lsServiceDescriptors;
        }

        public static bool IsServiceRegistered(ILogger logger, IConfiguration configuration, string endPoint)
        {
            var services = GetServiceDescriptors(logger, configuration);
            foreach (var service in services)
            {
                if (service.EndPoint == endPoint)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class ServiceDescriptor
    {
        public string Id { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string ServiceName { get; set; } = "Service";
        public string ServiceType { get; set; } = "Service";        
        public string EndPoint { get; set; } = string.Empty;
        public string UploadEndPoint { get; set; } = string.Empty;
        public string BackEnd { get; set; } = string.Empty;
        public string TimeStamp { get; set; } = DateTime.Now.ToString();
    }
}
