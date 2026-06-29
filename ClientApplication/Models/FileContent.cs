namespace Europeana3D.Web.Models { 
    public class FileContent
    {
        public string Name { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Base64Data { get; set; } 
    }
}
