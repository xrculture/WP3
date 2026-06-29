namespace Europeana3D.Web.Models
{
    public class UploadViewModel
    {
        public IFormFile? LocalFile { get; set; }
        public string? SourceUrl { get; set; }

        // ServiceName of the target repository: "Zenodo" | "AWS S3"
        public string TargetRepositoryId { get; set; } = string.Empty;

        // User-supplied credentials (source="user" params from Repositories.xml)
        public string? AccessToken { get; set; }   // Zenodo
        public string? BucketName { get; set; }    // S3
        public string? FolderPath { get; set; }    // S3 subfolder (optional)
        public string? Community { get; set; }     // Zenodo community (optional)

        // Metadata
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Creator { get; set; }
        public string? Rights { get; set; }
        public string? License { get; set; }
        public string? PublicationYear { get; set; }
        public string? Tags { get; set; }   // comma-separated

        public bool PublishImmediately { get; set; }
    }
}
