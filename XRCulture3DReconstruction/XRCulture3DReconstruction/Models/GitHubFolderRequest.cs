namespace XRCulture3DReconstruction.Models
{
    public class GitHubFolderRequest
    {
        public string Workflow { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Repo { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
    }
}
