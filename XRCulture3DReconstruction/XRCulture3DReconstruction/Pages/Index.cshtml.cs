using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serilog.Core;
using XRCulture3DReconstruction.Models;
using XRCulture3DReconstruction.Services;
using XRCulture3DReconstruction.Storage;
using XRCulture3DReconstruction.Workflows;

namespace XRCulture3DReconstruction.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly TaskLoggerFactory _loggerFactory;
        private readonly ISignalRLoggerService _signalRLogger;
        private readonly ISignalRStatusService _signalRStatus;
        private Logger? _taskLogger = null;

        public IndexModel(
            IConfiguration configuration,
            TaskLoggerFactory loggerFactory,
            ISignalRLoggerService signalRLogger, 
            ISignalRStatusService signalRStatus)
        {
            _configuration = configuration;
            _loggerFactory = loggerFactory;
            _signalRLogger = signalRLogger;
            _signalRStatus = signalRStatus;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnGetStatus()
        {
            var runningTasks = await _signalRStatus.GetRunningTasks();

            return new JsonResult(new
            {
                status = runningTasks.Count() > 0 ? "busy" : "idle",
                runningTasks,
            });
        }

        public async Task<IActionResult> OnGetWorkflows()
        {
            List<object> workflows = new()
            {
                new { id = "openMVG-openMVS", name = "openMVG-openMVS" }
            };

            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                workflows.Add(new { id = "NeRFStudio", name = "NeRFStudio" });
            }

            return new JsonResult(workflows);
        }

        public IActionResult OnPostProcessGitHubFolder([FromBody] GitHubFolderRequest request)
        {
            // Execute in the background
            _ = Task.Run(async () => await processGitHubFolder(request.Workflow, request.Owner, request.Repo, request.Branch, request.Folder, request.Options, "***SignalR-Log-Hub***"));

            return new JsonResult(new
            {
                success = true,
                message = "Processing started successfully.",
                groupName = "***SignalR-Log-Hub***"
            });
        }

        public async Task processGitHubFolder(string workflow, string owner, string repo, string branch, string folder, Dictionary<string, string> options, string groupName)
        {
            var taskId = Guid.NewGuid().ToString();
            var model = $"https://github.com/{owner}/{repo}/tree/{branch}/{folder}";

            _taskLogger = _loggerFactory.CreateTaskLogger(taskId);

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var modelsDir = _configuration[$"{fileStorage}:ModelsDir"]!;
            Directory.CreateDirectory(modelsDir);

            try
            {
                await _signalRStatus.AddTask(taskId, "***SignalR-Status-Hub***", workflow, model, "Running");

                using (var gitHub = new GitHub(_taskLogger, _signalRLogger, groupName))
                {
                    await gitHub.Download(owner, repo, branch, folder);
                    if (!string.IsNullOrEmpty(gitHub.DataPath))
                    {
                        if (workflow == "openMVG-openMVS")
                        {
                            var openMVG_openMVS_Workflow = new openMVG_openMVS(_configuration, _taskLogger, _signalRLogger, groupName, taskId, options);
                            if (await openMVG_openMVS_Workflow.Execute(model, gitHub.DataPath))
                            {
                                await _signalRStatus.CompleteTask(taskId, "", openMVG_openMVS_Workflow.ViewUrl, openMVG_openMVS_Workflow.DownloadUrl);
                                return;
                            }
                        }
                        else if (workflow == "NeRFStudio")
                        {
                            var NeRFStudioWorkflow = new NeRFStudio(_configuration, _taskLogger, _signalRLogger, groupName, taskId, options);
                            if (await NeRFStudioWorkflow.Execute(model, gitHub.DataPath))
                            {
                                await _signalRStatus.CompleteTask(taskId, "", NeRFStudioWorkflow.ViewUrl, NeRFStudioWorkflow.DownloadUrl);
                                return;
                            }
                        } 
                        else
                        {   await LogMessage("Invalid workflow specified.", true);
                            await _signalRStatus.CompleteTask(taskId, "Invalid workflow specified.", "", "");                
                            return;
                        }
                    }
                }

                _Workflow.SaveModelXml(openMVG_openMVS.NAME, taskId, model, modelsDir);
                await LogMessage("Internal error.", true);
                await _signalRStatus.CompleteTask(taskId, "Internal error.", "", "");                
            }
            catch (Exception ex)
            {
                _Workflow.SaveModelXml(openMVG_openMVS.NAME, taskId, model, modelsDir);
                await LogMessage(ex.Message, true);
                await _signalRStatus.CompleteTask(taskId, ex.Message, "", "");                
                return;
            }
            finally
            {
                _taskLogger.Dispose();
                _taskLogger = null;
            }
        }

        public async Task LogMessage(string message, bool error = false)
        {
            if (_taskLogger != null)
            {
                if (error)
                    _taskLogger.Error(message);
                else
                    _taskLogger.Information(message);
            }

            if (_signalRLogger != null && !string.IsNullOrEmpty("***SignalR-Log-Hub***"))
            {
                await _signalRLogger.SendLogMessage("***SignalR-Log-Hub***", $"{DateTime.Now:HH:mm:ss} [{(error ? "ERR" : "INF")}] {message}");
            }
        }
    }
}