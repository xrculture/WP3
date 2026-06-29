using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Europeana3D.Web.Models;
using Microsoft.Extensions.Options;
using System.Xml.Serialization;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;


namespace Europeana3D.Web.Services
{
    public class BridgeService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly EuropeanaService _europeana;
        private readonly XmlTemplateService _xml;
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public BridgeService(IHttpClientFactory httpClientFactory, EuropeanaService europeana, XmlTemplateService xml)
        {
            _httpClientFactory = httpClientFactory;
            _europeana = europeana;
            _xml = xml;
        }

        /// <summary>
        /// Creates middleware request XML and convertes back response XML.
        /// Saves the request XML under Resources via supplied saver.
        /// </summary>
        public async Task<List<EuropeanaItem>> SearchModelXML(string searchQuery)
        {

            // UI to Middleware for SearchRequest
            var tmpl = _xml.LoadModelRequestTemplate();
            var root = tmpl.Root!;

            var filters = root.Element("Filters")!
            .Element("Filter")!;
            filters.Element("SearchQuery")!.Value = searchQuery;

            root.Element("ServiceUrl")!.Value = "https://api.europeana.eu/record/v2/search.json";

            // --- Save the finalized XML to Resources before making the HTTP call ---
            _xml.SaveLocalXML(tmpl, Path.Combine(AppContext.BaseDirectory, "Resources"), "ModelRequest");
            
            // Send request to Europeana
            var xml = await ProcessAsync(tmpl);

            // Middleware to UI for SearchResponse
            var serializer = new XmlSerializer(typeof(ModelResponse));
            using var sr = new StringReader(xml.ToString());
            var items = (ModelResponse)serializer.Deserialize(sr)!;

            return items.EuropeanaItems;
            
        }

        /// <summary>
        /// Converts middleware request XML to a Europeana API call, returns response XML compatible with Middleware.
        /// Saves the response XML under Resources via supplied saver.
        /// </summary>
        public async Task<XDocument> ProcessAsync(XDocument requestXml)
        {
            // Connector Middleware to Europeana for SearchRequest

            var modelReq = requestXml.Root!;

            var serviceUrl = (string?)modelReq.Element("ServiceUrl") ?? throw new InvalidOperationException("ServiceUrl not found");
            var pagination = modelReq.Element("Pagination");
            var lengthAttr = pagination?.Attribute("length")?.Value;
            var rows = int.TryParse(lengthAttr, out var len) ? len : 24;


            var filters = modelReq.Element("Filters")?.Elements("Filter").FirstOrDefault();
            var searchQuery = filters?.Element("SearchQuery")?.Value ?? string.Empty;

            // Build Europeana query
           var query = new List<string>
                {
                $"query={Uri.EscapeDataString(searchQuery)}",
                "qf=TYPE:3D",
                "media=true",
                "profile=rich",
                $"rows={rows}"
                };
            var q = string.Join("&", query);          

            // Call Europeana
            var (success, items) = await _europeana.Search3DAsync($"{serviceUrl}?{q}");
            int statusCode = success ? (int)HttpStatusCode.OK : (int)HttpStatusCode.BadRequest;
            var response = new ModelResponse { Status= statusCode, Message= ReasonPhrases.GetReasonPhrase(statusCode), EuropeanaItems = items };

            // Connector Europeana to Middleware
            // Build response XML
            var serializer = new XmlSerializer(typeof(ModelResponse));
            using var sw = new StringWriter();
            serializer.Serialize(sw, response);
            string xml = sw.ToString();
            XDocument doc = XDocument.Parse(xml);


            // --- Save the finalized XML to Resources before making the HTTP call ---
            _xml.SaveLocalXML(doc, Path.Combine(AppContext.BaseDirectory, "Resources"), "ModelResponse");
            
            return doc;
        }
    }
}
