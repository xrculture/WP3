using System.Net.Http.Headers;
using XRCulture3DReconstruction.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace XRCulture3DReconstruction.Storage
{
    public class GitHub : IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly ISignalRLoggerService? _signalRLogger;
        private readonly string? _groupName;
        string? _extractPath = null;
        string? _dataPath = null;

        public GitHub(Serilog.ILogger logger, ISignalRLoggerService signalRLogger, string groupName)
        {
            _logger = logger;
            _signalRLogger = signalRLogger;
            _groupName = groupName;
        }

        public async Task Download(string owner, string repo, string branch, string folder)
        {
            try
            {
                var stopWatch = System.Diagnostics.Stopwatch.StartNew();

                await LogMessage("Starting GitHub download process...");

                Clean();

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromMinutes(60);
                        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("XRCulture", "1.0"));

                        await LogMessage("Downloading file list from GitHub...");

                        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{folder}?ref={branch}";

                        var getFileListResponse = await client.GetAsync(apiUrl);
                        if (!getFileListResponse.IsSuccessStatusCode)
                        {
                            await LogMessage($"GitHub API returned {getFileListResponse.StatusCode} for {apiUrl}", true);
                            return;
                        }

                        var files = System.Text.Json.JsonSerializer.Deserialize<List<GitHubFile>>(
                            await getFileListResponse.Content.ReadAsStringAsync(),
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (files != null)
                        {
                            await LogMessage($"Downloaded file list ({files.Count} files) in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
                            
                            _extractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                            _dataPath = Path.Combine(_extractPath, folder);
                            string destinationFolder = Path.Combine(_dataPath, "images");
                            Directory.CreateDirectory(destinationFolder);

                            var fileFiles = files.Where(f => f.Type == "file").ToList();
                            await LogMessage($"Starting download of {fileFiles.Count} files...");

                            int downloadedCount = 0;
                            foreach (var file in fileFiles)
                            {
                                if (file?.Name != null)
                                {
                                    string filePath = Path.Combine(destinationFolder, file.Name);
                                    using var getFileResponse = await client.GetAsync(file.Download_Url, HttpCompletionOption.ResponseHeadersRead);
                                    using var fileStream = new FileStream(filePath, FileMode.Create);
                                    await getFileResponse.Content.CopyToAsync(fileStream);

                                    downloadedCount++;
                                    await LogMessage($"Downloaded {file.Name} ({downloadedCount}/{fileFiles.Count})");
                                }
                            }

                            await LogMessage($"All files downloaded successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
                        }
                        else
                        {
                            await LogMessage("Error: failed to download file list.", true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogMessage($"Error: {ex.Message}", true);
                throw;
            }
        }

        public void Dispose()
        {
            Clean();
        }

        private void Clean()
        {
            try
            {
                if (!string.IsNullOrEmpty(_extractPath) && Directory.Exists(_extractPath))
                {
                    Directory.Delete(_extractPath, true);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error: {ex.Message}");
            }
            finally
            {
                _extractPath = null;
                _dataPath = null;
            }
        }

        private async Task LogMessage(string message, bool error = false)
        {
            _logger.Information(message);

            if (_signalRLogger != null && !string.IsNullOrEmpty(_groupName))
            {
                await _signalRLogger.SendLogMessage(_groupName, $"{DateTime.Now:HH:mm:ss} [{(error ? "ERR" : "INF")}] {message}");
            }
        }

        public string? DataPath => _dataPath;
    }

    public class GitHubFile
    {
        public string? Name { get; set; }
        public string? Download_Url { get; set; }
        public string? Type { get; set; }
    }
}