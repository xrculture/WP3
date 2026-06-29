namespace Europeana3D.Web.Models
{
    public class RepositoryOption
    {
        public string ProviderID { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string UploadEndpoint { get; set; } = string.Empty;
        public List<RepositoryUploadParameter> UploadParameters { get; set; } = new();
        public List<string> SupportedExtensions { get; set; } = new();
    }

    public class RepositoryUploadParameter
    {
        public string Name { get; set; } = string.Empty;
        public bool Required { get; set; }
        public string Source { get; set; } = string.Empty;  // "user" | "appsettings"
        public string Label { get; set; } = string.Empty;
    }
}
