using System.IO.Compression;
using System.Text;
using XRCulture3DReconstruction.Services;

namespace XRCulture3DReconstruction.Workflows
{
    public abstract class _Workflow
    {
        private readonly Serilog.ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly ISignalRLoggerService _signalRLogger;
        private readonly string _groupName;
        private readonly string _taskId;
        private readonly Dictionary<string, string>? _options;

        public _Workflow(IConfiguration configuration, Serilog.ILogger logger, ISignalRLoggerService signalRLogger, string groupName, string taskId, Dictionary<string, string>? options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _signalRLogger = signalRLogger ?? throw new ArgumentNullException(nameof(signalRLogger));
            _groupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
            _taskId = taskId;
            _options = options;
        }

        public int ExecuteProcess(string exePath, string args, string workingDirectory = "", int timeoutHours = 18, IEnumerable<string>? extraPathEntries = null)
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            // IIS worker processes do not inherit the interactive user PATH
            if (extraPathEntries != null)
            {
                var currentPath = process.StartInfo.EnvironmentVariables["PATH"] ?? string.Empty;
                var injected = string.Join(Path.PathSeparator.ToString(), extraPathEntries);
                process.StartInfo.EnvironmentVariables["PATH"] = $"{injected}{Path.PathSeparator}{currentPath}";
            }

            var outputBuilder = new System.Text.StringBuilder();
            var outputLock = new object();
            process.OutputDataReceived += async (sender, e) =>
            {
                if (e.Data != null)
                {
                    await LogMessage(e.Data);
                    lock (outputLock)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                }
            };

            var errorBuilder = new System.Text.StringBuilder();
            var errorLock = new object();
            process.ErrorDataReceived += async (sender, e) =>
            {
                if (e.Data != null)
                {
                    await LogMessage(e.Data);
                    lock (errorLock)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            int timeoutMilliseconds = timeoutHours * 60 * 60 * 1000;
            bool exited = process.WaitForExit(timeoutMilliseconds);
            if (!exited)
            {
                LogMessage($"Process timed out after {timeoutHours} hours. Killing process...").Wait();
                process.Kill(entireProcessTree: true);
                return -1;
            }

            return process.ExitCode;
        }

        public async Task PreProcessData(string dataFolder)
        {
            var imagesDir = Path.Combine(dataFolder, "images");

            // Check for mp4 files in the input folder
            var mp4Files = Directory.GetFiles(imagesDir, "*.mp4", SearchOption.TopDirectoryOnly);
            if (mp4Files.Length == 0)
            {
                await LogMessage("No mp4 file found in the input folder.");
                return;
            }

            if (mp4Files.Length > 1)
            {
                await LogMessage($"Multiple mp4 files found ({mp4Files.Length}). Expected only 1 mp4 file.", true);
                return;
            }

            var videoPath = mp4Files[0];
            await LogMessage($"Found video file: {Path.GetFileName(videoPath)}");

            // Convert video to images
            

            bool success = await ConvertVideoToImages(videoPath, imagesDir, fps: 2, quality: 2);
            if (success)
            {
                File.Delete(videoPath);
            }

            await LogMessage("Video conversion completed.");
        }

        // fps: Optional frame rate (0 = extract all frames, 1 = 1 frame/second, etc.)
        // quality: JPEG quality (2-31, lower is better quality)
        public async Task<bool> ConvertVideoToImages(string videoPath, string outputDir, int fps = 0, int quality = 2)
        {
            if (!File.Exists(videoPath))
            {
                await LogMessage($"Video file not found: {videoPath}", true);
                return false;
            }

            Directory.CreateDirectory(outputDir);

            var ffmpegPath = _configuration["ToolPaths:FFmpeg"];
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                await LogMessage("FFmpeg path not configured in appsettings.json", true);
                return false;
            }

            var ffmpegExe = Path.Combine(ffmpegPath, "ffmpeg.exe");
            if (!File.Exists(ffmpegExe))
            {
                await LogMessage($"FFmpeg executable not found: {ffmpegExe}", true);
                return false;
            }

            await LogMessage($"Converting video to images: {videoPath}");
            await LogMessage($"Output directory: {outputDir}");

            // Build FFmpeg arguments
            var fpsFilter = fps > 0 ? $"-vf fps={fps}" : "";
            var outputPattern = Path.Combine(outputDir, "frame_%04d.jpg");
            var args = $"-i \"{videoPath}\" {fpsFilter} -q:v {quality} \"{outputPattern}\"";

            await LogMessage($"FFmpeg command: {ffmpegExe} {args}");

            var exitCode = ExecuteProcess(ffmpegExe, args, extraPathEntries: [ffmpegPath]);
            if (exitCode != 0)
            {
                await LogMessage($"FFmpeg failed with exit code {exitCode}", true);
                return false;
            }

            var imageCount = Directory.GetFiles(outputDir, "*.jpg").Length;
            await LogMessage($"Successfully extracted {imageCount} frames");

            return true;
        }

        public async Task<HttpResponseMessage?> RetryPostAsync(HttpClient client, string url, HttpContent content, int maxRetries = 3)
        {
            int retryCount = 0;
            HttpResponseMessage? response = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    response = await client.PostAsync(url, content);
                    return response;
                }
                catch (HttpRequestException ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw;

                    int delayMs = (int)Math.Pow(2, retryCount) * 1000;
                    await Task.Delay(delayMs);

                    await LogMessage($"Retry {retryCount}/{maxRetries} for {url} after error: {ex.Message}");
                }
            }

            return response;
        }

        public async Task CreateZipArchive(string inputDir, string outputDir, string zipName)
        {
            string archivePath = Path.Combine(outputDir, zipName);

            await LogMessage($"Creating '{zipName}'...");

            using (var zipStream = new FileStream(archivePath, FileMode.Create))
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    // Add .bin and .jpg files
                    var files = Directory.EnumerateFiles(inputDir, "*.*", SearchOption.AllDirectories).Where(f =>
                        f.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));

                    foreach (var file in files)
                    {
                        await LogMessage($"Adding file to archive: {file}");

                        string entryName = Path.GetRelativePath(inputDir, file);
                        archive.CreateEntryFromFile(file, entryName);
                    }
                }
            }

            await LogMessage($"'{zipName}' created successfully.");
        }

        public async void UpdateLibrary(string model, string inputDir)
        {
            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";
            var sep = Path.DirectorySeparatorChar;

            string modelsDir = _configuration[$"{fileStorage}:ModelsDir"]!;
            Directory.CreateDirectory(modelsDir);

            if (!string.IsNullOrEmpty(inputDir))
            {
                await LogMessage($"Updating model library for {_taskId}...");

                string binzPath = Path.Combine(modelsDir, _taskId + ".binz");
                using (var zipStream = new FileStream(binzPath, FileMode.Create))
                {
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                    {
                        var files = Directory.EnumerateFiles(inputDir + $"{sep}obj", "*.*", SearchOption.AllDirectories)
                            .Where(
                            f => f.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

                        foreach (var file in files)
                        {
                            await LogMessage($"Adding file to archive: {file}");

                            string entryName = Path.GetRelativePath(inputDir + $"{sep}obj", file);
                            archive.CreateEntryFromFile(file, entryName);
                        }
                    }
                }

                ViewUrl = $"/Viewer?model={_taskId}.binz";
                DownloadUrl = $"/Storage?handler=Model&id={_taskId}.binz";
                LogUrl = $"/Storage?handler=Log&id={_taskId}.txt";

                await LogMessage($"Model library for {_taskId} updated successfully.");
            }

            SaveModelXml(Name, _taskId, model, modelsDir, _options);
        }

        public static void SaveModelXml(string workflow, string taskId, string model, string modelsDir, Dictionary<string, string>? options = null)
        {
            StringBuilder xml = new();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<model>");
            xml.AppendLine($"\t<input>{model}</input>");
            xml.AppendLine($"\t<workflow>");
            xml.AppendLine($"\t\t<name><![CDATA[{workflow}]]></name>");
            xml.AppendLine($"\t\t<parameters>");
            
            if (options != null)
            {
                foreach (var kvp in options)
                {
                    xml.AppendLine($"\t\t\t<{kvp.Key}>{kvp.Value}</{kvp.Key}>");
                }
            }
            
            xml.AppendLine($"\t\t</parameters>");
            xml.AppendLine($"\t</workflow>");
            xml.AppendLine($"\t<timeStamp>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</timeStamp>");
            xml.AppendLine("</model>");
            File.WriteAllText(Path.Combine(modelsDir, $"{taskId}.xml"), xml.ToString());
        }

        private static string StripAnsiCodes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove ANSI escape sequences
            return System.Text.RegularExpressions.Regex.Replace(input, @"\x1B\[[0-9;]*[a-zA-Z]", string.Empty);
        }

        public async Task LogMessage(string message, bool error = false)
        {
            string cleanMessage = StripAnsiCodes(message);

            if (error)
                _logger.Error(cleanMessage);
            else
                _logger.Information(cleanMessage);

            if (_signalRLogger != null && !string.IsNullOrEmpty(_groupName))
            {
                await _signalRLogger.SendLogMessage(_groupName, $"{DateTime.Now:HH:mm:ss} [{(error ? "ERR" : "INF")}] {cleanMessage}");
            }
        }

        public abstract string Name { get; }
        public Serilog.ILogger Logger => _logger;
        public IConfiguration Configuration => _configuration;
        public ISignalRLoggerService SignalRLoggerService => _signalRLogger;
        public string GroupName => _groupName;
        public string TaskId => _taskId;
        public string ViewUrl { get; private set; } = string.Empty;
        public string DownloadUrl { get; private set; } = string.Empty;
        public string LogUrl { get; private set; } = string.Empty;
        public Dictionary<string, string>? Options => _options;
    }
}