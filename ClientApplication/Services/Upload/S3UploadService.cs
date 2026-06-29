using Amazon.S3;
using Amazon.S3.Model;
using Europeana3D.Web.Models;
using Europeana3D.Web.Services.Upload;
using System.Xml.Linq;

namespace Europeana3D.Web.Services
{
    public class S3UploadService : IUploadService
    {
        private readonly IAmazonS3 _s3;
        private readonly XmlTemplateService _xml;
        private readonly IConfiguration _configuration;

        public string RepositoryName => "AWS S3";

        public S3UploadService(IAmazonS3 s3, XmlTemplateService xml, IConfiguration configuration)
        {
            _s3 = s3;
            _xml = xml;
            _configuration = configuration;
        }

        public async Task<ModelUploadResponse> UploadAsync(UploadViewModel model, Stream fileStream, string filename, long fileSize)
        {
            var region = _configuration["AWS:Region"] ?? "eu-west-1";
            var bucket = model.BucketName?.Trim() ?? string.Empty;
            var serviceUrl = $"https://s3.{region}.amazonaws.com";
            var dir = Path.Combine(AppContext.BaseDirectory, "Resources");
            var key = BuildS3Key(model.Title, model.FolderPath, filename);

            SaveRequestXml(model, serviceUrl, bucket, key, dir);

            var response = await DoUploadAsync(model, fileStream, key, region, bucket);

            SaveResponseXml(response, dir);

            return response;
        }

        private async Task<ModelUploadResponse> DoUploadAsync(
            UploadViewModel model, Stream fileStream, string key, string region, string bucket)
        {
            if (string.IsNullOrWhiteSpace(bucket))
                return Fail("MISSING_BUCKET", "S3 Bucket Name is required.");

            try
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = key,
                    InputStream = fileStream,
                    ContentType = "application/octet-stream",
                    AutoCloseStream = false
                };

                if (!string.IsNullOrWhiteSpace(model.Title))
                    putRequest.Metadata["x-amz-meta-title"] = model.Title;
                if (!string.IsNullOrWhiteSpace(model.Description))
                    putRequest.Metadata["x-amz-meta-description"] = model.Description;
                if (!string.IsNullOrWhiteSpace(model.Creator))
                    putRequest.Metadata["x-amz-meta-creator"] = model.Creator;
                if (!string.IsNullOrWhiteSpace(model.Rights))
                    putRequest.Metadata["x-amz-meta-rights"] = model.Rights;
                if (!string.IsNullOrWhiteSpace(model.License))
                    putRequest.Metadata["x-amz-meta-license"] = model.License;

                await _s3.PutObjectAsync(putRequest);

                return new ModelUploadResponse
                {
                    Status = 201,
                    Message = "Upload to S3 successful.",
                    UploadedModel = new UploadedModel
                    {
                        Id = key,
                        Title = model.Title,
                        Provider = "AWS S3",
                        RecordUrl = $"https://s3.console.aws.amazon.com/s3/object/{bucket}?region={region}&prefix={Uri.EscapeDataString(key)}",
                        DownloadUrl = $"https://{bucket}.s3.{region}.amazonaws.com/{Uri.EscapeDataString(key)}",
                        Rights = model.Rights,
                        PublicationYear = model.PublicationYear,
                        Published = true,
                        Timestamp = DateTime.UtcNow.ToString("o")
                    }
                };
            }
            catch (Exception ex)
            {
                return Fail("S3_EXCEPTION", ex.Message);
            }
        }

        private static string BuildS3Key(string title, string? folder, string originalFilename)
        {
            var ext = Path.GetExtension(originalFilename);
            var safeTitle = SanitizeFilename(title);
            folder = folder?.Trim().Trim('/');
            return string.IsNullOrEmpty(folder)
                ? $"{safeTitle}{ext}"
                : $"{folder}/{safeTitle}{ext}";
        }

        private static string SanitizeFilename(string title)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(title.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
        }

        private static ModelUploadResponse Fail(string code, string detail) => new()
        {
            Status = 500,
            Message = "Upload failed.",
            Errors = new() { new UploadError { Code = code, Detail = detail } }
        };

        private void SaveRequestXml(UploadViewModel model, string serviceUrl, string bucket, string key, string dir)
        {
            try
            {
                var doc = new XDocument(new XElement("ModelUploadRequest",
                    new XElement("TargetRepository",
                        new XElement("ServiceID", "s3"),
                        new XElement("ServiceUrl", serviceUrl),
                        new XElement("BucketName", bucket),
                        new XElement("FolderPath", model.FolderPath),
                        new XElement("S3Key", key)),
                    new XElement("Metadata",
                        new XElement("Title", model.Title),
                        new XElement("Description", model.Description),
                        new XElement("Creator", model.Creator),
                        new XElement("License", model.License),
                        new XElement("Rights", model.Rights),
                        new XElement("PublicationYear", model.PublicationYear),
                        new XElement("Tags", model.Tags))));

                _xml.SaveLocalXML(doc, dir, "ModelUploadRequest");
            }
            catch { /* non-fatal */ }
        }

        private void SaveResponseXml(ModelUploadResponse response, string dir)
        {
            try
            {
                XElement body = response.UploadedModel != null
                    ? new XElement("UploadedModel",
                        new XElement("Id", response.UploadedModel.Id),
                        new XElement("Provider", response.UploadedModel.Provider),
                        new XElement("DownloadUrl", response.UploadedModel.DownloadUrl),
                        new XElement("RecordUrl", response.UploadedModel.RecordUrl),
                        new XElement("Published", response.UploadedModel.Published),
                        new XElement("Timestamp", response.UploadedModel.Timestamp))
                    : new XElement("Errors",
                        response.Errors.Select(e =>
                            new XElement("Error",
                                new XElement("Code", e.Code),
                                new XElement("Detail", e.Detail))));

                var doc = new XDocument(new XElement("ModelUploadResponse",
                    new XElement("Status", response.Status),
                    new XElement("Message", response.Message),
                    body));

                _xml.SaveLocalXML(doc, dir, "ModelUploadResponse");
            }
            catch { /* non-fatal */ }
        }
    }
}
