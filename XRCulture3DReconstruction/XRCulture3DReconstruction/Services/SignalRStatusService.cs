using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using XRCulture3DReconstruction.Hubs;
using XRCulture3DReconstruction.Pages;
using XRCulture3DReconstruction.Workflows;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace XRCulture3DReconstruction.Services
{
    public record TaskStatus(
        string TaskId,
        string GroupName,
        string Workflow,
        string Model,       
        string Status = "Pending",
        string Error = "",
        int Progress = 0,
        string ViewUrl = "",
        string DownloadUrl = "",
        DateTime LastUpdated = default)
    {
        public TaskStatus() : this(string.Empty, string.Empty, string.Empty, string.Empty) { }
    }

    public interface ISignalRStatusService
    {
        Task AddTask(string taskId, string groupName, string workflow, string model, string status);
        Task UpdateTask(string taskId, string status);
        Task CompleteTask(string taskId, string error, string viewUrl, string downloadUrl);
        Task RemoveTask(string taskId);
        Task<ICollection<TaskStatus>> GetTasks();
        Task<TaskStatus?> GetTask(string taskId);
        Task<IList<TaskStatus>> GetRunningTasks();
        Task<int> GetActiveConnectionsCount(string groupName);
        Task SendStatusUpdate(string groupName, string message);
        Task SendStatusUpdate(string groupName, string message, object data);
    }

    public class SignalRStatusService : ISignalRStatusService
    {
        private readonly IConfiguration _configuration;
        private readonly IHubContext<StatusHub> _hubContext;
        private readonly IConnectionStateService _connectionState;
        private readonly TaskLoggerFactory _loggerFactory;
        private readonly ISignalRLoggerService _signalRLogger;
        private readonly ConcurrentDictionary<string, TaskStatus> _tasks = new();

        private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Completed", "Failed", "Cancelled", "Error"
        };

        private static readonly HashSet<string> RunningStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Running", "Processing", "Uploading"
        };

        public SignalRStatusService(
            IConfiguration configuration, 
            IHubContext<StatusHub> hubContext, 
            IConnectionStateService connectionState, 
            TaskLoggerFactory loggerFactory,
            ISignalRLoggerService signalRLogger)
        {
            _configuration = configuration;
            _hubContext = hubContext;
            _connectionState = connectionState;
            _loggerFactory = loggerFactory;
            _signalRLogger = signalRLogger;

            LoadTasks();

            _ = Task.Run(async () => await ExecuteTask());
        }

        public async Task AddTask(string taskId, string groupName, string workflow, string model, string status)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                throw new ArgumentException("TaskId cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException("groupName cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(model))
            {
                throw new ArgumentException("Model cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(status))
            {
                throw new ArgumentException("Status cannot be null or empty.");
            }

            var newTask = new TaskStatus(taskId, groupName, workflow, model,  status, "", 0, "", "", DateTime.UtcNow);
            if (!_tasks.TryAdd(taskId, newTask))
            {
                throw new InvalidOperationException($"Task with ID '{taskId}' already exists.");
            }

            await SendTaskStatusUpdate(newTask);
        }

        public async Task UpdateTask(string taskId, string status)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                throw new ArgumentException("TaskId cannot be null or empty.");
            }

            TaskStatus? taskStatusUpdated = null;

            try
            {
                _tasks.AddOrUpdate(
                    taskId,
                    // Add factory
                    key => throw new InvalidOperationException($"Task with ID '{taskId}' not found."),
                    // Update factory
                    (key, existingTask) =>
                    {
                        taskStatusUpdated = existingTask with
                        {
                            Status = status,
                            LastUpdated = DateTime.UtcNow
                        };
                        return taskStatusUpdated;
                    });

                if (taskStatusUpdated != null)
                {
                    await SendTaskStatusUpdate(taskStatusUpdated);
                }
            }
            catch (InvalidOperationException)
            {
                // Task not found
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to complete task '{taskId}': {ex.Message}", ex);
            }
        }

        public async Task CompleteTask(string taskId, string error, string viewUrl, string downloadUrl)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                throw new ArgumentException("TaskId cannot be null or empty.");
            }

            TaskStatus? taskStatusUpdated = null;

            try
            {
                _tasks.AddOrUpdate(
                    taskId,
                    // Add factory
                    key => throw new InvalidOperationException($"Task with ID '{taskId}' not found."),
                    // Update factory
                    (key, existingTask) =>
                    {
                        // Already in terminal state
                        if (TerminalStatuses.Contains(existingTask.Status))
                        {
                            return existingTask;
                        }

                        taskStatusUpdated = existingTask with
                        {
                            Status = string.IsNullOrEmpty(error) ? "Completed" : "Error",
                            Error = error ?? string.Empty,
                            ViewUrl = viewUrl ?? string.Empty,
                            DownloadUrl = downloadUrl ?? string.Empty,
                            Progress = string.IsNullOrEmpty(error) ? 100 : existingTask.Progress,
                            LastUpdated = DateTime.UtcNow
                        };
                        return taskStatusUpdated;
                    });

                if (taskStatusUpdated != null)
                {
                    var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
                    var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

                    var tasksDir = _configuration[$"{fileStorage}:TasksDir"]!;
                    var taskXMLPath = Path.Combine(tasksDir, $"{taskId}.xml");
                    var serializer = new XmlSerializer(typeof(Services.TaskStatus));
                    using var stream = System.IO.File.Create(taskXMLPath);
                    serializer.Serialize(stream, taskStatusUpdated);

                    await SendTaskStatusUpdate(taskStatusUpdated);
                }
            }
            catch (InvalidOperationException)
            {
                // Task not found
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to complete task '{taskId}': {ex.Message}", ex);
            }
        }

        public async Task RemoveTask(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                throw new ArgumentException("TaskId cannot be null or empty.");
            }

            bool taskRemoved = _tasks.TryRemove(taskId, out var taskStatus);
            if (taskRemoved && (taskStatus != null))
            {
                string statusJSON = JsonConvert.SerializeObject(new
                {
                    status = _tasks.IsEmpty ? "idle" : "busy",
                    tasks = _tasks
                }, Newtonsoft.Json.Formatting.Indented);

                await SendStatusUpdate(taskStatus.GroupName, statusJSON);
            }
        }

        public Task<ICollection<TaskStatus>> GetTasks()
        {
            return Task.FromResult(_tasks.Values);
        }

        public Task<TaskStatus?> GetTask(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                throw new ArgumentException("TaskId cannot be null or empty.");
            }

            _tasks.TryGetValue(taskId, out var taskStatus);
            return Task.FromResult(taskStatus);
        }

        public Task<IList<TaskStatus>> GetRunningTasks()
        {
            var runningTasks = _tasks.Values
                .Where(t => !string.IsNullOrEmpty(t.Status) && RunningStatuses.Contains(t.Status))
                .ToList();

            return Task.FromResult<IList<TaskStatus>>(runningTasks);
        }

        public async Task<int> GetActiveConnectionsCount(string groupName)
        {
            var connections = await _connectionState.GetConnectionsInGroup(groupName);
            return connections.Count();
        }

        public async Task SendStatusUpdate(string groupName, string message)
        {
            var connections = await _connectionState.GetConnectionsInGroup(groupName);
            if (connections.Any())
            {
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveStatusUpdate", message);
            }
        }

        public async Task SendStatusUpdate(string groupName, string message, object data)
        {
            var connections = await _connectionState.GetConnectionsInGroup(groupName);
            if (connections.Any())
            {
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveStatusUpdate", message, data);
            }
        }

        private async Task SendTaskStatusUpdate(TaskStatus taskStatus)
        {
            var runningTasks = _tasks.Values
                .Where(t => !string.IsNullOrEmpty(t.Status) && RunningStatuses.Contains(t.Status))
                .ToList();

            string statusJSON = JsonConvert.SerializeObject(new
            {
                status = runningTasks.Count() > 0 ? "busy" : "idle",
                runningTasks,
                taskStatusUpdate = taskStatus,
            }, Newtonsoft.Json.Formatting.Indented);

            await SendStatusUpdate(taskStatus.GroupName, statusJSON);
        }

        private void LoadTasks()
        {
            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var provider = new PhysicalFileProvider(_configuration[$"{fileStorage}:TasksDir"]!);
            var taskXMLs = provider.GetDirectoryContents("/").Where((fileInfo) =>
            {
                if (fileInfo.IsDirectory)
                    return false;

                if (!fileInfo.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            });

            List<TaskStatus> xmlTasks = new();
            foreach (var taskXML in taskXMLs)
            {
                if (taskXML?.PhysicalPath != null)
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(taskXML.PhysicalPath);

                    var id = Path.GetFileNameWithoutExtension(taskXML.Name);
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    _tasks.TryAdd(id, new TaskStatus
                    {
                        TaskId = id,
                        GroupName = xmlDoc.SelectSingleNode("//TaskStatus/GroupName")?.InnerText ?? "Unknown",
                        Model = xmlDoc.SelectSingleNode("//TaskStatus/Model")?.InnerText ?? "Unknown",
                        Status = xmlDoc.SelectSingleNode("//TaskStatus/Status")?.InnerText ?? "Pending",
                        Error = xmlDoc.SelectSingleNode("//TaskStatus/Error")?.InnerText ?? "",
                        Progress = int.TryParse(xmlDoc.SelectSingleNode("//TaskStatus/Progress")?.InnerText, out var progress) ? progress : 0,
                        ViewUrl = xmlDoc.SelectSingleNode("//TaskStatus/ViewUrl")?.InnerText ?? "",
                        DownloadUrl = xmlDoc.SelectSingleNode("//TaskStatus/DownloadUrl")?.InnerText ?? "",
                        LastUpdated = xmlDoc.SelectSingleNode("//TaskStatus/LastUpdated")?.InnerText != null
                            ? DateTime.Parse(xmlDoc.SelectSingleNode("//TaskStatus/LastUpdated")!.InnerText)
                            : DateTime.UtcNow
                    });
                }
            }
        }

        public async Task ExecuteTask()
        {
            while (true)
            {
                var runningTasks = _tasks.Values
                .Where(t => !string.IsNullOrEmpty(t.Status) && RunningStatuses.Contains(t.Status))
                .ToList();
                if (runningTasks.Count == 0)
                {
                    var pendingTasks = _tasks.Values.Where(t => string.Equals(t.Status, "Pending", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (pendingTasks.Count > 0)
                    {
                        var taskStatus = pendingTasks.First();

                        var taskLogger = _loggerFactory.CreateTaskLogger(taskStatus.TaskId);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await RunTask(taskLogger, taskStatus);
                            }
                            catch (Exception ex)
                            {
                                await LogMessage(taskLogger, $"Error executing task {taskStatus.TaskId}: {ex.Message}", true);
                            }
                            finally
                            {
                                taskLogger?.Dispose();
                                taskLogger = null;
                            }
                        });
                    }
                }

                // Check for tasks every 5 minutes
                await Task.Delay(1000 * 60 * 5); 
            }
        }

        public async Task RunTask(Logger taskLogger, TaskStatus taskStatus)
        {
            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var tasksDir = _configuration[$"{fileStorage}:TasksDir"]!;
            var dataZipPath = Path.Combine(tasksDir, $"{taskStatus.TaskId}.zip");

            var modelsDir = _configuration[$"{fileStorage}:ModelsDir"]!;
            Directory.CreateDirectory(modelsDir);

            // Extract zip
            var dataPath = Path.Combine(Path.GetTempPath(), taskStatus.TaskId);
            var extractDir = Path.Combine(dataPath, "images");
            Directory.CreateDirectory(extractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(dataZipPath, extractDir, overwriteFiles: true);            

            try
            {
                await UpdateTask(taskStatus.TaskId, "Running");

                if (!string.IsNullOrEmpty(dataPath))
                {
                    if (string.IsNullOrEmpty(taskStatus.Workflow) /*backward compatibility*/ ||
                        taskStatus.Workflow == "openMVG-openMVS")
                    {
                        var openMVG_openMVS_Workflow = new openMVG_openMVS(_configuration, taskLogger, _signalRLogger, "***SignalR-Log-Hub***", taskStatus.TaskId, null);
                        if (await openMVG_openMVS_Workflow.Execute(taskStatus.Model, dataPath))
                        {
                            await CompleteTask(taskStatus.TaskId, "", openMVG_openMVS_Workflow.ViewUrl, openMVG_openMVS_Workflow.DownloadUrl);
                            return;
                        }
                    }
                    else if (taskStatus.Workflow == "NeRFStudio")
                    {
                        var NeRFStudioWorkflow = new NeRFStudio(_configuration, taskLogger, _signalRLogger, "***SignalR-Log-Hub***", taskStatus.TaskId, null);
                        if (await NeRFStudioWorkflow.Execute(taskStatus.Model, dataPath))
                        {
                            await CompleteTask(taskStatus.TaskId, "", NeRFStudioWorkflow.ViewUrl, NeRFStudioWorkflow.DownloadUrl);
                            return;
                        }
                    }
                    else
                    {
                        await LogMessage(taskLogger, "Invalid workflow specified.", true);
                        await CompleteTask(taskStatus.TaskId, "Invalid workflow specified.", "", "");
                        return;
                    }
                }

                _Workflow.SaveModelXml(openMVG_openMVS.NAME, taskStatus.TaskId, taskStatus.Model, modelsDir);
                await LogMessage(taskLogger, "Internal error.", true);
                await CompleteTask(taskStatus.TaskId, "Internal error.", "", "");
            }
            catch (Exception ex)
            {
                _Workflow.SaveModelXml(openMVG_openMVS.NAME, taskStatus.TaskId, taskStatus.Model, modelsDir);
                await LogMessage(taskLogger, ex.Message, true);
                await CompleteTask(taskStatus.TaskId, ex.Message, "", "");
                return;
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(dataPath) && Directory.Exists(dataPath))
                    {
                        Directory.Delete(dataPath, true);
                    }
                }
                catch (Exception ex)
                {
                    await LogMessage(taskLogger, ex.Message, true);
                }
            }
        }

        public async Task LogMessage(Logger? taskLogger, string message, bool error = false)
        {
            if (taskLogger != null)
            {
                if (error)
                    taskLogger.Error(message);
                else
                    taskLogger.Information(message);
            }

            if (_signalRLogger != null)
            {
                await _signalRLogger.SendLogMessage("***SignalR-Log-Hub***", $"{DateTime.Now:HH:mm:ss} [{(error ? "ERR" : "INF")}] {message}");
            }
        }
    }
}