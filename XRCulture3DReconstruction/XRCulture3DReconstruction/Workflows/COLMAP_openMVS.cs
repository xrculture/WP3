using Microsoft.Extensions.Configuration;
using XRCulture3DReconstruction.Services;
using XRCulture3DReconstruction.Storage;

namespace XRCulture3DReconstruction.Workflows
{
    public class COLMAP_openMVS : _Workflow
    {
        public COLMAP_openMVS(IConfiguration configuration, Serilog.ILogger logger, ISignalRLoggerService signalRLogger, string groupName, string taskId, Dictionary<string, string>? options)
            : base(configuration, logger, signalRLogger, groupName, taskId, options)
        {
        }

        public async Task Execute(string dataPath)
        {
            //#todo
            throw new NotImplementedException();

            //var openMVS = new openMVS(Configuration, Logger, SignalRLoggerService, GroupName);
            //await openMVS.Execute(dataPath);
        }

        public override string Name => "COLMAP-openMVS";
    }
}
