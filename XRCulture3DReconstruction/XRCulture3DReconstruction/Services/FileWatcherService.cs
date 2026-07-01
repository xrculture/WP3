using Microsoft.AspNetCore.SignalR;
using XRCulture3DReconstruction.Hubs;

namespace XRCulture3DReconstruction.Services
{
    public class FileWatcherService : BackgroundService
    {
        private readonly ILogger<FileWatcherService> _logger;
        private readonly IHubContext<LibraryHub> _hubContext;
        private readonly IConfiguration _configuration;
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;
        private readonly object _lock = new();

        public FileWatcherService(
            ILogger<FileWatcherService> logger,
            IHubContext<LibraryHub> hubContext,
            IConfiguration configuration)
        {
            _logger = logger;
            _hubContext = hubContext;
            _configuration = configuration;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var modelsDir = _configuration[$"{fileStorage}:ModelsDir"]!;
            if (!Directory.Exists(modelsDir))
            {
                _logger.LogWarning("Models directory does not exist: {ModelsDir}", modelsDir);
                return Task.CompletedTask;
            }

            _watcher = new FileSystemWatcher(modelsDir)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnError;

            stoppingToken.Register(Dispose);

            return Task.CompletedTask;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!IsTrackedFile(e.Name))
                return;

            _logger.LogInformation("Library file {ChangeType}: {Name}", e.ChangeType, e.Name);
            ScheduleNotification();
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (!IsTrackedFile(e.Name) && !IsTrackedFile(e.OldName))
                return;

            _logger.LogInformation("Library file renamed: {OldName} -> {Name}", e.OldName, e.Name);
            ScheduleNotification();
        }

        private void OnError(object sender, ErrorEventArgs e) =>
            _logger.LogError(e.GetException(), "FileSystemWatcher error");

        private void ScheduleNotification()
        {
            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(NotifyClients, null, TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
            }
        }

        private void NotifyClients(object? state)
        {
            _logger.LogInformation("Notifying library clients of file system change");
            _ = _hubContext.Clients.All.SendAsync("libraryChanged");
        }

        private static bool IsTrackedFile(string? name) =>
            name != null &&
            (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(".binz", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

        public override void Dispose()
        {
            _debounceTimer?.Dispose();
            _watcher?.Dispose();
            base.Dispose();
        }
    }
}