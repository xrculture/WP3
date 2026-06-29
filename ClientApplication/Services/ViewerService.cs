using System.Xml.Linq;
using System.Xml.Serialization;
using Europeana3D.Web.Models;
using Newtonsoft.Json;

namespace Europeana3D.Web.Services
{
    public class ViewerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly XmlTemplateService _xml;
        private readonly IConfiguration _configuration;

        public ViewerService(IHttpClientFactory httpClientFactory, XmlTemplateService xml, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _xml = xml;
            _configuration = configuration;
        }

        public async Task<List<Viewer>> LoadViewersAsync(string? extension = null)
        {
            var source = _configuration["Viewers:Source"] ?? "local";

            if (source.Equals("remote", StringComparison.OrdinalIgnoreCase))
                return await LoadViewersFromRemoteAsync(extension);

            return LoadViewersFromXml(extension);
        }

        private List<Viewer> LoadViewersFromXml(string? extension)
        {
            return LoadViewersFromXml(_xml.LoadViewersXml(), extension);
        }

        private static List<Viewer> LoadViewersFromXml(XDocument xdoc, string? extension)
        {
            var viewersQuery = xdoc.Root!.Elements("Viewer");

            if (!string.IsNullOrEmpty(extension))
            {
                viewersQuery = viewersQuery.Where(v =>
                {
                    var formats = v.Element("BackEnd")?
                        .Element("SupportedOptions")?
                        .Element("FileFormats")?
                        .Elements("Format");
                    if (formats == null) return false;
                    return formats.Any(f => string.Equals((string?)f.Attribute("extension"), extension, StringComparison.OrdinalIgnoreCase));
                });
            }

            return viewersQuery.Select(v => new Viewer
            {
                ProviderID = (string?)v.Element("Id") ?? string.Empty,
                ServiceName = (string?)v.Element("ServiceName") ?? string.Empty,
                Endpoint = (string?)v.Element("EndPoint") ?? string.Empty,
                SessionToken = ((string?)v.Element("SessionToken") ?? string.Empty).Trim(),
                Protocols = v.Element("BackEnd")?.Element("SupportedOptions")?.Element("Protocols")?.Elements("Protocol")?.Select(e => e.Value).ToList() ?? new()
            })
            .ToList();
        }

        private async Task<List<Viewer>> LoadViewersFromRemoteAsync(string? extension)
        {
            var endpoint = _configuration["Viewers:RemoteEndpoint"] ?? "";
            var client = _httpClientFactory.CreateClient();
            var xml = await client.GetStringAsync(endpoint);
            var xdoc = XDocument.Parse(xml);
            return LoadViewersFromXml(xdoc, extension);
        }


        public async Task<(bool ok, string message)> PostModelLoadingAsync(string? modelUrl, FileContent? fileContent, Viewer viewer,
            long? contentLength, string? ext, string? action)
        {
            
            // Build payload from template (ModelLoading.xml)
            var tmpl = _xml.LoadModelLoadingTemplate();
            var root = tmpl.Root!;


            root.Element("ServiceID")!.Value = viewer.ProviderID; // fill service id
            root.Element("SessionToken")!.Value = viewer.SessionToken; // fill token
            root.Element("oEmbed")!.Value = action == "oEmbed" ? "True" : "False";

            var src = root.Element("Source")!;

            if (!string.IsNullOrEmpty(modelUrl))
            {
                var urlSrc = src.Element("UrlSource")!;
                urlSrc.Element("FileExtension")!.Value = ext;
                urlSrc.Element("FileDimension")!.Value = (contentLength ?? 0).ToString();
                urlSrc.Element("Url")!.Value = modelUrl; // direct URL
            } else if (fileContent != null)
            {
                var localSrc = src.Element("LocalSource")!;
                localSrc.SetAttributeValue("dimension", (contentLength ?? 0).ToString());
                localSrc.SetAttributeValue("extension", ext);
                localSrc.SetAttributeValue("filename", fileContent.Filename);
                localSrc.SetAttributeValue("name", fileContent.Name);
                localSrc.SetAttributeValue("description", fileContent.Description ?? String.Empty);
                localSrc.Value = fileContent.Base64Data ?? String.Empty;

            }
            else
            {
                throw new Exception("No Local Source or Cloud Source provided!");
            }

            // Optional: tweak SceneInit defaults
            var sceneInit = root.Element("SceneInit")!;
            sceneInit.Element("Zoom")!.SetAttributeValue("default", "True");
            sceneInit.Element("Pan")!.SetAttributeValue("default", "True");
            sceneInit.Element("BackgroundColor")!.SetAttributeValue("default", "True");
            sceneInit.Element("View")!.SetAttributeValue("default", "True");
            sceneInit.Element("Lights")!.SetAttributeValue("default", "True");

            // --- Save the finalized XML to Resources before making the HTTP call ---
            _xml.SaveLocalXML(tmpl, Path.Combine(AppContext.BaseDirectory, "Resources"), "ModelLoading");
           
            // Now create the HTTP client and POST
            var client = _httpClientFactory.CreateClient();

            StringContent content;
            if (viewer.ServiceName.Contains("RDF")) 
                content = new StringContent(JsonConvert.SerializeObject(tmpl.ToString(), Formatting.Indented), System.Text.Encoding.UTF8, "application/xml");
            else
                content = new StringContent(tmpl.ToString(), System.Text.Encoding.UTF8, "application/xml");


            try
            {
                var resp = await client.PostAsync(viewer.Endpoint, content);
                var body = await resp.Content.ReadAsStringAsync();
                var response = body;
                if (resp.IsSuccessStatusCode)
                {
                    XDocument doc = XDocument.Parse(body);
                    // --- Save the finalized XML response to Resources before returning the view ---
                    _xml.SaveLocalXML(doc, Path.Combine(AppContext.BaseDirectory, "Resources"), "ModelLoadingResponse");

                    var status = doc.Root?.Element("Status") ?? throw new Exception("Status not found");
                    if (Convert.ToInt16(status!.Value) != (int)System.Net.HttpStatusCode.OK)
                        throw new Exception(doc.Root!.Element("Message")!.Value ?? "Unknown error");                       
                    var endpoint = doc.Root?.Element("Endpoint") ?? throw new Exception("Endpoint not found");
                    var viewerUrl = endpoint!.Value ?? throw new Exception("Endpoint empty");
                    response = viewerUrl;               
                }
                
                return (resp.IsSuccessStatusCode, response);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
