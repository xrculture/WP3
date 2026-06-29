using Europeana3D.Web.Models;
using Europeana3D.Web.Services.Upload;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace Europeana3D.Web.Services
{
    public class ZenodoUploadService : IUploadService
    {
        private readonly IHttpClientFactory _http;
        private readonly XmlTemplateService _xml;

        public string RepositoryName => "Zenodo";

        public ZenodoUploadService(IHttpClientFactory http, XmlTemplateService xml)
        {
            _http = http;
            _xml = xml;
        }

        public async Task<ModelUploadResponse> UploadAsync(UploadViewModel model, Stream fileStream, string filename, long fileSize)
        {
            const string baseUrl = "https://zenodo.org/api/deposit/depositions";
            var dir = Path.Combine(AppContext.BaseDirectory, "Resources");

            // Save request XML before any repository call
            SaveRequestXml(model, baseUrl, dir);

            var response = await DoUploadAsync(model, fileStream, filename, baseUrl);

            // Save response XML after repository responds — success or error
            SaveResponseXml(response, dir);

            return response;
        }

        private async Task<ModelUploadResponse> DoUploadAsync(
            UploadViewModel model, Stream fileStream, string filename, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(model.AccessToken))
                return Fail("MISSING_TOKEN", "Zenodo Personal Access Token is required.");

            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", model.AccessToken);

            try
            {
                // 1. Create empty deposition
                var createResp = await client.PostAsync(baseUrl,
                    new StringContent("{}", Encoding.UTF8, "application/json"));
                if (!createResp.IsSuccessStatusCode)
                    return Fail("ZENODO_CREATE_FAILED", await createResp.Content.ReadAsStringAsync());

                var createJson = JObject.Parse(await createResp.Content.ReadAsStringAsync());
                var depositId = createJson["id"]!.Value<long>();
                var bucketUrl = createJson["links"]!["bucket"]!.Value<string>()!;
                var recordUrl = $"https://zenodo.org/deposit/{depositId}";

                // 2. Upload file to bucket
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                var uploadResp = await client.PutAsync($"{bucketUrl}/{filename}", fileContent);
                if (!uploadResp.IsSuccessStatusCode)
                    return Fail("ZENODO_UPLOAD_FAILED", await uploadResp.Content.ReadAsStringAsync());

                var uploadJson = JObject.Parse(await uploadResp.Content.ReadAsStringAsync());
                var downloadUrl = uploadJson["links"]?["self"]?.Value<string>() ?? string.Empty;

                // 3. Set metadata
                var keywords = (model.Tags ?? "")
                    .Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToArray();

                var metaObj = new JObject
                {
                    ["title"] = model.Title,
                    ["description"] = !string.IsNullOrWhiteSpace(model.Description) ? model.Description : model.Title,
                    ["upload_type"] = "dataset",
                    ["access_right"] = "open",
                    ["license"] = !string.IsNullOrWhiteSpace(model.License) ? model.License.ToLowerInvariant() : "cc-by",
                    ["creators"] = new JArray { new JObject { ["name"] = !string.IsNullOrWhiteSpace(model.Creator) ? model.Creator : "Unknown" } },
                    ["keywords"] = new JArray(keywords.Cast<object>())
                };

                if (!string.IsNullOrWhiteSpace(model.PublicationYear))
                    metaObj["publication_date"] = $"{model.PublicationYear}-01-01";

                if (!string.IsNullOrWhiteSpace(model.Community))
                    metaObj["communities"] = new JArray { new JObject { ["identifier"] = model.Community } };

                var metaResp = await client.PutAsync($"{baseUrl}/{depositId}",
                    new StringContent(new JObject { ["metadata"] = metaObj }.ToString(), Encoding.UTF8, "application/json"));
                if (!metaResp.IsSuccessStatusCode)
                    return Fail("ZENODO_METADATA_FAILED", await metaResp.Content.ReadAsStringAsync());

                // 4. Publish if requested
                bool published = false;
                if (model.PublishImmediately)
                {
                    var pubResp = await client.PostAsync($"{baseUrl}/{depositId}/actions/publish",
                        new StringContent(string.Empty));
                    if (pubResp.IsSuccessStatusCode)
                    {
                        published = true;
                        recordUrl = $"https://zenodo.org/record/{depositId}";
                    }
                }

                return new ModelUploadResponse
                {
                    Status = 201,
                    Message = published ? "Upload and publication successful." : "Upload successful. Record saved as draft.",
                    UploadedModel = new UploadedModel
                    {
                        Id = depositId.ToString(),
                        Title = model.Title,
                        Provider = "Zenodo",
                        RecordUrl = recordUrl,
                        DownloadUrl = downloadUrl,
                        Rights = model.Rights,
                        PublicationYear = model.PublicationYear,
                        Published = published,
                        Timestamp = DateTime.UtcNow.ToString("o")
                    }
                };
            }
            catch (Exception ex)
            {
                return Fail("EXCEPTION", ex.Message);
            }
        }

        private static ModelUploadResponse Fail(string code, string detail) => new()
        {
            Status = 500,
            Message = "Upload failed.",
            Errors = new() { new UploadError { Code = code, Detail = detail } }
        };

        private void SaveRequestXml(UploadViewModel model, string serviceUrl, string dir)
        {
            try
            {
                var doc = new XDocument(new XElement("ModelUploadRequest",
                    new XElement("TargetRepository",
                        new XElement("ServiceID", "zenodo"),
                        new XElement("ServiceUrl", serviceUrl),
                        new XElement("AccessToken", "***")),
                    new XElement("Metadata",
                        new XElement("Title", model.Title),
                        new XElement("Description", model.Description),
                        new XElement("Creator", model.Creator),
                        new XElement("License", model.License),
                        new XElement("Rights", model.Rights),
                        new XElement("PublicationYear", model.PublicationYear),
                        new XElement("Tags", model.Tags)),
                    new XElement("UploadOptions",
                        new XElement("PublishImmediately",
                            new XAttribute("value", model.PublishImmediately)))));

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
                        new XElement("RecordUrl", response.UploadedModel.RecordUrl),
                        new XElement("DownloadUrl", response.UploadedModel.DownloadUrl),
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
