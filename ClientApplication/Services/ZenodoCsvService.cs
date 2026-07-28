using System.Text;

namespace Europeana3D.Web.Services
{
    public class ZenodoCsvService
    {
        private static readonly string[] ThumbExtensions = { "png", "jpg", "jpeg", "webp", "gif" };
        private readonly IHttpClientFactory _http;
        private readonly string[] _csvFilenames;
        private readonly string _csvPid;

        public ZenodoCsvService(IHttpClientFactory http, IConfiguration config)
        {
            _http = http;
            _csvPid = config.GetSection("Options:CsvPidField").Get<string>() ?? "edm:pid";
            _csvFilenames = config.GetSection("Options:MetadataCsvFilename").Get<string[]>()
                ?? new[] { config["Options:MetadataCsvFilename"] ?? "metadata.csv" };
        }

        // Tries each configured filename in order; returns content of the first one found.
        public async Task<string?> FetchCsvAsync(string recordId, string? accessToken = null)
        {
            var client = _http.CreateClient();
            foreach (var filename in _csvFilenames)
            {
                var url = BuildFileUrl(recordId, filename, accessToken);
                var resp = await client.SendAsync(BuildRequest(HttpMethod.Get, url));
                if (resp.IsSuccessStatusCode)
                    return await resp.Content.ReadAsStringAsync();
            }
            return null;
        }

        // Returns true if any of the configured CSV filenames exists in the record.
        public async Task<bool> MetadataCsvExistsAsync(string recordId, string? accessToken = null)
        {
            var client = _http.CreateClient();
            foreach (var filename in _csvFilenames)
            {
                var url = BuildFileUrl(recordId, filename, accessToken);
                try
                {
                    var resp = await client.SendAsync(BuildRequest(HttpMethod.Head, url));
                    if (resp.IsSuccessStatusCode) return true;
                }
                catch { /* try next */ }
            }
            return false;
        }

        public List<Dictionary<string, string>> ParseCsv(string content)
        {
            var lines = content.ReplaceLineEndings("\n")
                               .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return new();

            var headers = SplitCsvLine(lines[0]);
            var rows = new List<Dictionary<string, string>>();

            for (int i = 1; i < lines.Length; i++)
            {
                var values = SplitCsvLine(lines[i]);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < headers.Count && j < values.Count; j++)
                    row[headers[j]] = values[j];
                rows.Add(row);
            }

            return rows;
        }

        public Dictionary<string, string>? FindRowByPid(
            List<Dictionary<string, string>> rows, string pid) =>
            rows.FirstOrDefault(r =>
                r.TryGetValue(_csvPid, out var p) &&
                p.Equals(pid, StringComparison.OrdinalIgnoreCase));

        public (string[] Headers, string[] Values)? FindRowRaw(string content, string pid)
        {
            var lines = content.ReplaceLineEndings("\n")
                               .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return null;
            var headers = SplitCsvLine(lines[0]).ToArray();
            int pidIdx = Array.FindIndex(headers, h => h.Equals(_csvPid, StringComparison.OrdinalIgnoreCase));
            if (pidIdx < 0) return null;
            for (int i = 1; i < lines.Length; i++)
            {
                var values = SplitCsvLine(lines[i]).ToArray();
                if (pidIdx < values.Length && values[pidIdx].Equals(pid, StringComparison.OrdinalIgnoreCase))
                    return (headers, values);
            }
            return null;
        }

        public async Task<string?> ProbeThumbAsync(string recordId, string pid, string? accessToken = null)
        {
            var client = _http.CreateClient();
            foreach (var ext in ThumbExtensions)
            {
                var url = BuildFileUrl(recordId, $"{pid}_thumb.{ext}", accessToken);
                try
                {
                    var resp = await client.SendAsync(BuildRequest(HttpMethod.Head, url));
                    if (resp.IsSuccessStatusCode) return url;
                }
                catch { /* try next */ }
            }
            return null;
        }

        private static string BuildFileUrl(string recordId, string filename, string? accessToken)
        {
            var url = $"https://zenodo.org/api/records/{recordId}/files/{filename}/content?download=1";
            return string.IsNullOrEmpty(accessToken) ? url : $"{url}&access_token={accessToken}";
        }

        private static HttpRequestMessage BuildRequest(HttpMethod method, string url)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            req.Headers.TryAddWithoutValidation("User-Agent", "XRCultureClientApp/1.0");
            return req;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            int i = 0;
            while (true)
            {
                if (i > line.Length) break;
                if (i == line.Length) { fields.Add(""); break; }

                if (line[i] == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            i++;
                            if (i < line.Length && line[i] == '"') { sb.Append('"'); i++; }
                            else break;
                        }
                        else sb.Append(line[i++]);
                    }
                    fields.Add(sb.ToString());
                    if (i < line.Length && line[i] == ';') i++;
                }
                else
                {
                    int sep = line.IndexOf(';', i);
                    if (sep < 0) { fields.Add(line[i..]); break; }
                    fields.Add(line[i..sep]);
                    i = sep + 1;
                }
            }
            return fields;
        }
    }
}
