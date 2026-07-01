using Serilog;
using Serilog.Core;

namespace XRCulture3DReconstruction
{
    public class TaskLoggerFactory
    {
        private readonly string _modelsDir;

        public TaskLoggerFactory(IConfiguration configuration)
        {
            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            _modelsDir = configuration[$"{fileStorage}:ModelsDir"]
                ?? throw new InvalidOperationException("Models path is not configured.");
            Directory.CreateDirectory(_modelsDir);
        }

        public Logger CreateTaskLogger(string taskId)
        {
            var logFileName = $"{taskId}.txt";
            var logFilePath = Path.Combine(_modelsDir, logFileName);

            return new LoggerConfiguration()
                .WriteTo.File(logFilePath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        public Logger CreateTaskLogger(string taskId, string taskName)
        {
            var sanitizedTaskName = string.Join("_", taskName.Split(Path.GetInvalidFileNameChars()));
            var logFileName = $"task_{sanitizedTaskName}_{taskId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var logFilePath = Path.Combine(_modelsDir, "tasks", logFileName);

            Directory.CreateDirectory(Path.Combine(_modelsDir, "tasks"));

            return new LoggerConfiguration()
                .WriteTo.File(logFilePath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
    }
}