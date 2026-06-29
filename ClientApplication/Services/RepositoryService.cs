using Europeana3D.Web.Models;
using System.Xml.Linq;

namespace Europeana3D.Web.Services
{
    public class RepositoryService
    {
        private readonly XmlTemplateService _xml;

        public RepositoryService(XmlTemplateService xml) => _xml = xml;

        public List<RepositoryOption> GetAll()
        {
            var xdoc = _xml.LoadRepositoriesXml();
            return xdoc.Root!.Elements("Repository").Select(ParseRepository).ToList();
        }

        public RepositoryOption? GetByName(string serviceName) =>
            GetAll().FirstOrDefault(r => r.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        private static RepositoryOption ParseRepository(XElement r)
        {
            var uploadParams = r.Element("BackEnd")?
                .Element("SupportedOptions")?
                .Element("UploadParameters")?
                .Elements("Parameter")
                .Select(p => new RepositoryUploadParameter
                {
                    Name = (string?)p.Attribute("name") ?? string.Empty,
                    Required = (string?)p.Attribute("required") == "True",
                    Source = (string?)p.Attribute("source") ?? "user",
                    Label = (string?)p.Attribute("label") ?? string.Empty
                }).ToList() ?? new();

            var supportedExts = r.Element("BackEnd")?
                .Element("SupportedOptions")?
                .Element("FileFormats")?
                .Elements("Format")
                .Select(f => ((string?)f.Attribute("extension") ?? "").TrimStart('.').ToLowerInvariant())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList() ?? new();

            return new RepositoryOption
            {
                ProviderID = (string?)r.Element("ProviderID") ?? string.Empty,
                ServiceName = (string?)r.Element("ServiceName") ?? string.Empty,
                Endpoint = (string?)r.Element("Endpoint") ?? string.Empty,
                UploadEndpoint = (string?)r.Element("UploadEndpoint") ?? string.Empty,
                UploadParameters = uploadParams,
                SupportedExtensions = supportedExts
            };
        }
    }
}
