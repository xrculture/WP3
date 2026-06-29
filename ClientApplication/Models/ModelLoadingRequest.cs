using System.Xml.Serialization;

namespace Europeana3D.Web.Models
{
    [XmlRoot("ModelLoadingRequest")]
    public class ModelLoadingRequest
    {
        public string ServiceID { get; set; } = string.Empty; // use ProviderID
        public string SessionToken { get; set; } = string.Empty; // from Viewer
        public Source Source { get; set; } = new();
        public SceneInit SceneInit { get; set; } = new();
    }

    public class Source
    {
        public UrlSource UrlSource { get; set; } = new();
    }


    public class UrlSource
    {
        public string FileExtension { get; set; } = string.Empty; // .obj / .ifc / .dae
        public string FileDimension { get; set; } = string.Empty; // content length
        public string Url { get; set; } = string.Empty; // direct file URL
    }


    public class SceneInit
    {
        [XmlAttribute] public bool Zoom { get; set; } = true;
        [XmlAttribute] public bool Pan { get; set; } = true;
        [XmlAttribute] public bool BackgroundColor { get; set; } = true;
        [XmlAttribute] public bool View { get; set; } = true;
        [XmlAttribute] public bool Lights { get; set; } = true;
    }
}
