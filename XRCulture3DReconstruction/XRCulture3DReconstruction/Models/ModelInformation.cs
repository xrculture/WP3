namespace XRCulture3DReconstruction.Models
{
    public class ModelInformation
    {
        public string? Id { get; set; }
        public string? Input { get; set; }
        public string? WorkflowName { get; set; }
        public string? TimeStamp { get; set; }
        public string? ViewUrl { get; set; }
        public string? DownloadUrl { get; set; }
        public string? LogUrl { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();

        public ModelView View { get; set; } = new();
    }
}
