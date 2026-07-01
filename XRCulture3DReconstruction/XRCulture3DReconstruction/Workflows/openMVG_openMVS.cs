using XRCulture3DReconstruction.Services;
using XRCulture3DReconstruction.Storage;

namespace XRCulture3DReconstruction.Workflows
{
    public class openMVG_openMVS : _Workflow
    {
        public static readonly string NAME = "openMVG-openMVS";

        public openMVG_openMVS(IConfiguration configuration, Serilog.ILogger logger, ISignalRLoggerService signalRLogger, string groupName, string taskId, Dictionary<string, string>? options)
            : base(configuration, logger, signalRLogger, groupName, taskId, options)
        {
        }

        public async Task<bool> Execute(string model, string dataPath)
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            await LogMessage($"*** {Name} Workflow started...");

            await PreProcessData(dataPath);

            var openMVG = new openMVG(Configuration, Logger, SignalRLoggerService, GroupName, TaskId, Options);
            if (await openMVG.Execute(dataPath))
            {
                var openMVS = new openMVS(Configuration, Logger, SignalRLoggerService, GroupName, TaskId, Options);
                if (await openMVS.Execute(dataPath))
                {
                    UpdateLibrary(model, dataPath);

                    await LogMessage($"*** {Name} Workflow completed after {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
                    return true;
                }
            }
            await LogMessage($"*** {Name} Workflow failed after {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            return false;
        }

        public override string Name => NAME;
    }
}
