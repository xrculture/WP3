using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Serilog.Core;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;
using XRCulture3DReconstruction.Services;
using XRCulture3DReconstruction.Storage;
using XRCulture3DReconstruction.Workflows;

namespace XRCulture3DReconstruction.Pages
{
    [IgnoreAntiforgeryToken]
    public class TaskManagerModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ISignalRLoggerService _signalRLogger;
        private readonly ISignalRStatusService _signalRStatus;

        public TaskManagerModel(
            IConfiguration configuration,
            ISignalRLoggerService signalRLogger, 
            ISignalRStatusService signalRStatus)
        {
            _configuration = configuration;
            _signalRLogger = signalRLogger;
            _signalRStatus = signalRStatus;
        }

        public async Task<IActionResult> OnGet()
        {
            var runningTasks = await _signalRStatus.GetRunningTasks();

            return new JsonResult(new
            {
                status = runningTasks.Count() > 0 ? "busy" : "idle",
                runningTasks,
            });
        }

        public async Task<IActionResult> OnGetTaskStatus()
        {
            var taskId = Request.Query["taskId"];
            if (string.IsNullOrEmpty(taskId))
            {
                return Content(GetTaskStatusHTTPResponse.BadRequest.Replace("%MESSAGE%", "Missing TaskId parameter."), "application/xml");
            }

            var taskStatus = await _signalRStatus.GetTask(taskId!);
            if (taskStatus == null)
            {
                return Content(GetTaskStatusHTTPResponse.NotFound.Replace("%MESSAGE%", "Task not found."), "application/xml");
            }

            return new JsonResult(taskStatus);
        }

        public async Task<IActionResult> OnPostCreate3DModelAsync()
        {
            if (!Request.HasFormContentType)
            {
                return Content(Create3DModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Content-Type must be multipart/form-data."), "application/xml");
            }

            var form = await Request.ReadFormAsync();

            // 
            // XML part
            //

            string? xmlPart = null;

            var xmlRequest = form.Files.FirstOrDefault(f => f.Name == "request");
            if (xmlRequest != null)
            {
                using var reader = new StreamReader(xmlRequest.OpenReadStream());
                xmlPart = await reader.ReadToEndAsync();
            }
            else if (form.TryGetValue("request", out var xmlField))
            {
                xmlPart = xmlField.ToString();
            }

            if (string.IsNullOrEmpty(xmlPart))
            {
                return Content(Create3DModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Missing XML request part."), "application/xml");
            }

            //
            // ZIP part
            //

            var zipFile = form.Files.FirstOrDefault(f => f.Name == "file");
            if ((zipFile == null) || (zipFile.Length == 0))
            {
                return Content(Create3DModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Missing or empty zip file."), "application/xml");
            }

            // Parse XML
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlPart);

            var model = xmlDoc.SelectSingleNode("//Create3DModelRequest/Model")?.InnerText?.Trim();
            if (string.IsNullOrEmpty(model))
            {
                return Content(Create3DModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Bad request: 'Model'."), "application/xml");
            }

            var workflow = xmlDoc.SelectSingleNode("//Create3DModelRequest/Workflow")?.InnerText;
            if (string.IsNullOrEmpty(workflow))
            {
                return Content(Create3DModelHTTPResponse.BadRequest.Replace("%MESSAGE%", "Bad request: 'Workflow'."), "application/xml");
            }

            // Data ZIP
            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var tasksDir = _configuration[$"{fileStorage}:TasksDir"]!;
            Directory.CreateDirectory(tasksDir);

            var taskId = Guid.NewGuid().ToString();
            var dataZipPath = Path.Combine(tasksDir, $"{taskId}.zip");
            using (var fileStream = System.IO.File.Create(dataZipPath))
            {
                using (var zipStream = zipFile.OpenReadStream())
                {
                    await zipStream.CopyToAsync(fileStream);
                }
            }
            
            // Task XML
            var taskStatus = new Services.TaskStatus(
                taskId,
                "***SignalR-Status-Hub***",
                workflow,
                model,
                "Pending",
                LastUpdated: DateTime.UtcNow);
            var taskXMLPath = Path.Combine(tasksDir, $"{taskId}.xml");
            var serializer = new XmlSerializer(typeof(Services.TaskStatus));
            using var stream = System.IO.File.Create(taskXMLPath);
            serializer.Serialize(stream, taskStatus);

            await _signalRStatus.AddTask(taskId, "***SignalR-Status-Hub***", workflow, model, "Pending");

            return Content(Create3DModelHTTPResponse.SuccessWithParameters.Replace("%PARAMETERS%", $"<TaskId>{taskId}</TaskId>"), "application/xml");
        }

        public async Task LogMessage(string message, bool error = false)
        {
            if (_signalRLogger != null)
            {
                await _signalRLogger.SendLogMessage("***SignalR-Log-Hub***", $"{DateTime.Now:HH:mm:ss} [{(error ? "ERR" : "INF")}] {message}");
            }
        }
    }
}
