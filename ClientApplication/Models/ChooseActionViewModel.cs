namespace Europeana3D.Web.Models
{
    public class ChooseActionViewModel
    {
        public string EuropeanaId { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Preview { get; set; }
        public string? FileExtension { get; set; }
        public long? FileSize { get; set; }

        public List<string> SupportedUrls { get; set; } = new();
        public string? SelectedUrl { get; set; }


        // 1) Download, 2) Viewer
        public string SelectedAction { get; set; } = "download";


        // Viewer selection
        public List<ViewerOption> Viewers { get; set; } = new();
        public string? SelectedViewerProviderId { get; set; }
    }

    public record ViewerOption(string ProviderID, string DisplayName, bool oEmbed = false);
}
