using Europeana3D.Web.Models;

namespace Europeana3D.Web.Services.Upload
{
    public interface IUploadService
    {
        string RepositoryName { get; }
        Task<ModelUploadResponse> UploadAsync(UploadViewModel model, Stream fileStream, string filename, long fileSize);
    }
}
