using System.Xml.Serialization;

namespace Europeana3D.Web.Models
{

    [XmlRoot("ModelResponse")]
    public  class ModelResponse
    {
        [XmlElement] public int Status { get; set; }
        [XmlElement] public string Message { get; set; } = string.Empty;

        [XmlArray("Models")]
        [XmlArrayItem("Model")]
        public List<EuropeanaItem> EuropeanaItems { get; set; } = new();
    }

    
    public class EuropeanaItem
    {
        [XmlElement]  public string Id { get; set; } = string.Empty; // europeana_id
        [XmlElement]  public string? Title { get; set; }
        [XmlElement]  public string? Provider { get; set; }
        [XmlElement]  public string? Rights { get; set; }
        [XmlArray("Previews")]
        [XmlArrayItem("Preview")]
        public List<string> Previews { get; set; } = new();

        [XmlIgnore]
        public string? Preview => Previews.FirstOrDefault();


        // Candidate downloadable URLs (from edmIsShownBy / edmHasView)
        [XmlArray("MediaUrls")]
        [XmlArrayItem("Url")]
        public List<string> MediaUrls { get; set; } = new();


        // Validated 3D files we support with headers info
        [XmlArray("Supported3DFiles")]
        [XmlArrayItem("FileCandidate")]
        public List<FileCandidate> Supported3DFiles { get; set; } = new();

        [XmlIgnore]
        public string? FirstSupportedUrl => Supported3DFiles.FirstOrDefault()?.Url;

    }

    public class FileCandidate
    {
        private string _url = string.Empty;
        private string? _extension;
        private bool _extensionManuallySet = false;

        [XmlElement] public string Url {
            get => _url;
            set
            {
                _url = value;
                // Update Extension automatically only if not manually set
                if (!_extensionManuallySet)
                    _extension = GetExtensionFromUrl(_url);
            }
        } 
        [XmlElement] public string? ContentType { get; set; }
        [XmlElement] public long? ContentLength { get; set; }

        [XmlElement]
        public string Extension
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_extension))
                    return _extension;
                return GetExtensionFromUrl(_url);
            }

            set
            {
                _extension = value ?? string.Empty;
                _extensionManuallySet = true;
            }
        }

        [XmlIgnore]
        public string PrettySize => ContentLength is null or < 0 ? "?" :
        (ContentLength < 1024 ? $"{ContentLength} B" :
        ContentLength < 1024 * 1024 ? $"{ContentLength / 1024.0:0.#} KB" : $"{ContentLength / (1024.0 * 1024):0.##} MB");

        private static string GetExtensionFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            try
            {
                return Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
