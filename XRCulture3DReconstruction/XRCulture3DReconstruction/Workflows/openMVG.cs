using Microsoft.AspNetCore.Mvc;
using XRCulture3DReconstruction.Services;

namespace XRCulture3DReconstruction.Workflows
{
    public class openMVG : _Workflow
    {
        public openMVG(IConfiguration configuration, Serilog.ILogger logger, ISignalRLoggerService signalRLogger, string groupName, string taskId, Dictionary<string, string>? options)
            : base(configuration, logger, signalRLogger, groupName, taskId, options)
        {
        }

        public async Task<bool> Execute(string inputDir)
        {
            return await StructureFromMotion(inputDir);
        }

        private async Task<bool> StructureFromMotion(string inputDir)
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            await LogMessage("openMVG: Structure from Motion started...");

            var quality = Options?.GetValueOrDefault("quality", "High") ?? "Ultra";

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var toolPaths = isLinuxPlatform ? "ToolPathsLinux" : "ToolPaths";
            var sep = Path.DirectorySeparatorChar;

            {
                await LogMessage("*** Intrinsics analysis started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVG"] + (isLinuxPlatform ? "/openMVG_main_SfMInit_ImageListing" : @"\openMVG_main_SfMInit_ImageListing.exe");
                var args = $"--imageDirectory {inputDir}{sep}images --outputDirectory {inputDir}{sep}matches --camera_model 3 --group_camera_model 1 -f 1920";

                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"openMVG_main_SfMInit_ImageListing failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Intrinsics analysis completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** Compute features started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVG"] + (isLinuxPlatform ? "/openMVG_main_ComputeFeatures" : @"\openMVG_main_ComputeFeatures.exe");
                var args = $"--input_file {inputDir}{sep}matches{sep}sfm_data.json --outdir {inputDir}{sep}matches --describerMethod \"SIFT\" --describerPreset \"{quality.ToUpper()}\" --numThreads 0";
                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"openMVG_main_ComputeFeatures failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Compute features completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** Compute matching pairs started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVG"] + (isLinuxPlatform ? "/openMVG_main_PairGenerator" : @"\openMVG_main_PairGenerator.exe");
                var args = $"--input_file {inputDir}{sep}matches{sep}sfm_data.json --output_file {inputDir}{sep}matches{sep}pairs.bin";
                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"openMVG_main_PairGenerator failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Compute matching pairs completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** Compute matches started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVG"] + (isLinuxPlatform ? "/openMVG_main_ComputeMatches" : @"\openMVG_main_ComputeMatches.exe");
                var args = $"--input_file {inputDir}{sep}matches{sep}sfm_data.json --pair_list {inputDir}{sep}matches{sep}pairs.bin --output_file {inputDir}{sep}matches{sep}matches.putative.bin --nearest_matching_method AUTO --ratio 0.8";
                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"openMVG_main_ComputeMatches failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Compute matches completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** Filter matches (INCREMENTAL) started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVG"] + (isLinuxPlatform ? "/openMVG_main_GeometricFilter" : @"\openMVG_main_GeometricFilter.exe");
                var args = $"--input_file {inputDir}{sep}matches{sep}sfm_data.json --matches {inputDir}{sep}matches{sep}matches.putative.bin -g f --output_file {inputDir}{sep}matches{sep}matches.f.bin --max_iteration 2048";
                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"openMVG_main_GeometricFilter failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Filter matches completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** Reconstruction (INCREMENTAL) started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVG"] + (isLinuxPlatform ? "/openMVG_main_SfM" : @"\openMVG_main_SfM.exe");
                var args = $"--sfm_engine \"INCREMENTAL\" --input_file {inputDir}{sep}matches{sep}sfm_data.json --match_dir {inputDir}{sep}matches --output_dir {inputDir}{sep}reconstruction --triangulation_method 0";
                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"openMVG_main_SfM failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Reconstruction completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** Structure from Known Poses (Add remaining images) started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVG"] + (isLinuxPlatform ? "/openMVG_main_ComputeStructureFromKnownPoses" : @"\openMVG_main_ComputeStructureFromKnownPoses.exe");
                var args = $"--input_file {inputDir}{sep}reconstruction{sep}sfm_data.bin --match_dir {inputDir}{sep}matches --output_file {inputDir}{sep}reconstruction{sep}robust.bin --match_file {inputDir}{sep}matches{sep}matches.f.bin";
                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
            {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"openMVG_main_ComputeStructureFromKnownPoses failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Structure from Known Poses (Add remaining images) completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** Colorize structure started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVG"] + (isLinuxPlatform ? "/openMVG_main_ComputeSfM_DataColor" : @"\openMVG_main_ComputeSfM_DataColor.exe");
                var args = $"--input_file {inputDir}{sep}reconstruction{sep}robust.bin --output_file {inputDir}{sep}reconstruction{sep}colorized.ply";
                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"openMVG_main_ComputeSfM_DataColor failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Colorize structure completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            await LogMessage($"openMVG: - Structure from Motion completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");

            return true;
        }

        public override string Name => "openMVG";
    }
}
