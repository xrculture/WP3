using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Europeana3D.Web.Models;

namespace Europeana3D.Web.Services
{
    public class EuropeanaService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private IConfiguration _configuration;

        public EuropeanaService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            
        }


        public async Task<(bool success, List<EuropeanaItem>)> Search3DAsync(string query, CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient();


            // Europeana Search API – filter to 3D items with resolvable media
            // qf=TYPE:3D & media=true & profile=rich
            var url = $"{query}&wskey={_configuration.GetValue<string>("Options:EuropeanaApiKey")}";


            var resp = await client.GetFromJsonAsync<EuropeanaSearchResponse>(url, cancellationToken: ct);
            var success = resp?.Success ?? false;
            var results = new List<EuropeanaItem>();
            if (!success || resp?.Items == null) return (success, results);


            foreach (var it in resp.Items)
            {
                var item = new EuropeanaItem
                {
                    Id = it.Id ?? string.Empty,
                    Title = it.Title?.FirstOrDefault(),
                    Provider = it.DataProvider?.FirstOrDefault(),
                    Rights = it.Rights?.FirstOrDefault(),
                    Previews = it.Preview ?? new()
                };


                var candidates = new List<string>();
                if (it.EdmIsShownBy != null) candidates.AddRange(it.EdmIsShownBy);
                if (it.EdmHasView != null) candidates.AddRange(it.EdmHasView);


                item.MediaUrls = candidates.Distinct().ToList();

                // Keep only direct links ending supported formats
                var re = new Regex(@"\.(" + _configuration.GetValue<string>("Options:SupportedFormats") + @")(\?.*)?$", RegexOptions.IgnoreCase);
                var extFiltered = item.MediaUrls.Where(u => re.IsMatch(u)).Distinct().ToList();


                // Validate headers (HEAD; fallback to GET headers) and filter by mime/size
                var validated = new List<FileCandidate>();
                foreach (var urlCandidate in extFiltered)
                {
                    var fc = await ValidateAsync(urlCandidate, ct);
                    if (fc != null)
                        validated.Add(fc);
                }
                item.Supported3DFiles = validated;


                if (item.Supported3DFiles.Any())
                    results.Add(item);
            }
            return (true, results);
        }

        public async Task<FileCandidate?> ValidateAsync(string url, CancellationToken ct)
        {
            var http = _httpClientFactory.CreateClient();
            try
            {
                using var head = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResp = await http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!headResp.IsSuccessStatusCode)
                {
                    // Fallback to lightweight GET for headers
                    using var getResp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!getResp.IsSuccessStatusCode) return null;
                    return BuildCandidate(url, getResp.Content.Headers.ContentType?.MediaType, getResp.Content.Headers.ContentLength);
                }
                return BuildCandidate(url, headResp.Content.Headers.ContentType?.MediaType, headResp.Content.Headers.ContentLength);
            }
            catch
            {
                return null;
            }
        }

        private static FileCandidate? BuildCandidate(string url, string? contentType, long? contentLength)
        {
            // Basic checks: size present and > 0 preferred, but allow missing when extension is correct
            var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();


            // var okMime = IsMimeAcceptable(ext, contentType);
            var okSize = contentLength is null or > 0; // allow unknown size


            //if (!okMime || !okSize) return null;
            if (!okSize) return null;

            return new FileCandidate
            {
                Url = url,
                ContentType = contentType,
                ContentLength = contentLength
            };
        }

        /*
        private static bool IsMimeAcceptable(string ext, string? mime)
        {
            if (string.IsNullOrEmpty(mime)) return true; // some servers omit it; rely on extension
            mime = mime.ToLowerInvariant();
            return ext switch
            {
                ".obj" => mime is "model/obj" or "text/plain" or "application/octet-stream",
                ".dae" => mime is "model/vnd.collada+xml" or "application/xml" or "text/xml",
                ".ifc" => mime is "application/ifc" or "model/ifc" or "application/octet-stream" or "application/x-step" or "model/step" or "application/zip",
                ".glb" => mime is "model/gltf-binary",
                _ => false
            };
        }
        */
    }
}
