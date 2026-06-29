using System.Xml.Serialization;

namespace Europeana3D.Web.Models
{
    [XmlRoot("ModelUploadResponse")]
    public class ModelUploadResponse
    {
        [XmlElement] public int Status { get; set; }
        [XmlElement] public string Message { get; set; } = string.Empty;
        [XmlElement] public UploadedModel? UploadedModel { get; set; }

        [XmlArray("Errors")]
        [XmlArrayItem("Error")]
        public List<UploadError> Errors { get; set; } = new();
    }

    public class UploadedModel
    {
        [XmlElement] public string Id { get; set; } = string.Empty;
        [XmlElement] public string Title { get; set; } = string.Empty;
        [XmlElement] public string Provider { get; set; } = string.Empty;
        [XmlElement] public string RecordUrl { get; set; } = string.Empty;
        [XmlElement] public string DownloadUrl { get; set; } = string.Empty;
        [XmlElement] public string? Rights { get; set; }
        [XmlElement] public string? PublicationYear { get; set; }
        [XmlElement] public bool Published { get; set; }
        [XmlElement] public string Timestamp { get; set; } = string.Empty;
    }

    public class UploadError
    {
        [XmlElement] public string Code { get; set; } = string.Empty;
        [XmlElement] public string Detail { get; set; } = string.Empty;
    }
}
