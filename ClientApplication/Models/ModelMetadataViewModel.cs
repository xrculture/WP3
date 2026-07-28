namespace Europeana3D.Web.Models
{
    public class ModelMetadataViewModel
    {
        public string Pid { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? ThumbUrl { get; set; }
        public string ZenodoRecordId { get; set; } = string.Empty;
        public List<(string Column, string Value)> Fields { get; set; } = new();
        public string? ErrorMessage { get; set; }

        // Landing page (ConversionController path)
        public bool LandingPage { get; set; }
        public string? MetaJson { get; set; }
        public string? ModelUrl { get; set; }
        public string? FileExtension { get; set; }
        public List<ViewerOption> Viewers { get; set; } = new();

        // Serialized JSON map of all LandingPage CSV field names (from appsettings)
        public string LandingFieldsJson { get; set; } = "{}";
    }
}
