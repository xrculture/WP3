using System.Xml.Serialization;

namespace Europeana3D.Web.Models
{
    [XmlRoot(ElementName = "Protocol", Namespace = "http://example.com/service-protocol")]
    public class ViewersProtocol
    {
        [XmlElement("Viewer", Namespace = "http://example.com/service-protocol")]
        public List<Viewer> Viewers { get; set; } = new();
    }

    public class Viewer
    {
        [XmlElement] public string ProviderID { get; set; } = string.Empty;
        [XmlElement] public string ServiceName { get; set; } = string.Empty;
        [XmlElement] public string Endpoint { get; set; } = string.Empty;
        [XmlElement] public string SessionToken { get; set; } = string.Empty;
        
        [XmlArray("Protocols")]
        [XmlArrayItem("Protocol")]
        public List<string> Protocols { get; set; } = new();


        // Optional: formats, options, etc. not strictly needed for the post
        public override string ToString() => $"{ServiceName} ({ProviderID})";
    }
}
