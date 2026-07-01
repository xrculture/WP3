using System.Reflection;
using System.Text;
using XRCulture3DReconstruction.Services;

namespace XRCulture3DReconstruction.Workflows
{
    public class NeRFStudio : _Workflow
    {
        public static readonly string NAME = "NeRFStudio";

        public NeRFStudio(IConfiguration configuration, Serilog.ILogger logger, ISignalRLoggerService signalRLogger, string groupName, string taskId, Dictionary<string, string>? options)
            : base(configuration, logger, signalRLogger, groupName, taskId, options)
        {
        }

        public async Task<bool> Execute(string model, string dataPath)
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            await LogMessage($"*** {Name} Workflow started...");

            var quality = Options?.GetValueOrDefault("quality", "High") ?? "High";
            var matchingMethod = quality switch
            {
                "Normal" => "sequential",
                "High" => "vocab_tree",
                "Ultra" => "exhaustive",
                _ => "vocab_tree"
            };

            await PreProcessData(dataPath);

            // Batch file
            {
                await LogMessage($"*** {Name} processing started...");

                var anacondaPath = Configuration["ToolPaths:Anaconda"];
                var timeStamp = DateTime.Now.ToString("yyyy-MM-ddHHmmss");
                var cachePath = Path.Combine(dataPath, ".cache");

                StringBuilder batchFileBuilder = new();
                batchFileBuilder.AppendLine("@echo off");
                batchFileBuilder.AppendLine("chcp 65001 >nul");
                batchFileBuilder.AppendLine("set PYTHONIOENCODING=utf-8");
                batchFileBuilder.AppendLine("set PYTHONUNBUFFERED=1");
                batchFileBuilder.AppendLine($"set TORCH_HOME={Path.Combine(cachePath, "torch")}");
                batchFileBuilder.AppendLine($"set XDG_CACHE_HOME={cachePath}");
                batchFileBuilder.AppendLine($"set MPLCONFIGDIR={Path.Combine(cachePath, "matplotlib")}");
                batchFileBuilder.AppendLine($"set TEMP={Path.Combine(dataPath, "temp")}");
                batchFileBuilder.AppendLine($"set TMP={Path.Combine(dataPath, "temp")}");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine($"cd /d \"{dataPath}\"");
                batchFileBuilder.AppendLine("if %ERRORLEVEL% neq 0 exit /b 1");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine("echo Starting NeRFStudio workflow at %DATE% %TIME%");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine($"call \"{anacondaPath}\\Scripts\\activate.bat\" \"{anacondaPath}\"");
                batchFileBuilder.AppendLine("if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine("call conda activate nerfstudio");
                batchFileBuilder.AppendLine("if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine($"set PATH=%CONDA_PREFIX%;%CONDA_PREFIX%\\Library\\bin;%CONDA_PREFIX%\\Scripts;%CONDA_PREFIX%\\Library\\mingw-w64\\bin;%PATH%");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine("REM Add DLL search paths for PyAV dependencies");
                batchFileBuilder.AppendLine($"set PATH=%CONDA_PREFIX%\\Lib\\site-packages\\av;%PATH%");
                batchFileBuilder.AppendLine();

                // COLMAP processing with quality-focused parameters
                batchFileBuilder.AppendLine("echo Processing data with enhanced quality settings...");
                batchFileBuilder.AppendLine($"set PATH={Configuration["ToolPaths:FFmpeg"]};%PATH%");
                batchFileBuilder.AppendLine($"call ns-process-data images ^");
                batchFileBuilder.AppendLine($"  --data \"{dataPath}\\images\" ^");
                batchFileBuilder.AppendLine($"  --output-dir \"{dataPath}\\output\\SfM\" ^");
                batchFileBuilder.AppendLine($"  --matching-method {matchingMethod} ^");
                batchFileBuilder.AppendLine($"  --feature-type sift");
                batchFileBuilder.AppendLine("if %ERRORLEVEL% neq 0 (");
                batchFileBuilder.AppendLine("    echo COLMAP processing failed - insufficient image matches");
                batchFileBuilder.AppendLine("    exit /b %ERRORLEVEL%");
                batchFileBuilder.AppendLine(")");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine("REM Verify COLMAP produced valid results");
                batchFileBuilder.AppendLine($"if not exist \"{dataPath}\\output\\SfM\\transforms.json\" (");
                batchFileBuilder.AppendLine("    echo ERROR: COLMAP failed to generate transforms.json");
                batchFileBuilder.AppendLine("    echo This usually means insufficient image overlap or poor image quality");
                batchFileBuilder.AppendLine("    exit /b 1");
                batchFileBuilder.AppendLine(")");
                batchFileBuilder.AppendLine();

                // High-quality training parameters
                batchFileBuilder.AppendLine("echo Training NeRF model with high-quality settings...");
                batchFileBuilder.AppendLine($"call ns-train nerfacto ^");
                batchFileBuilder.AppendLine($"  --data \"{dataPath}\\output\\SfM\" ^");
                batchFileBuilder.AppendLine($"  --output-dir \"{dataPath}\\output\" ^");
                batchFileBuilder.AppendLine($"  --timestamp \"{timeStamp}\" ^");
                batchFileBuilder.AppendLine($"  --vis tensorboard ^");
                batchFileBuilder.AppendLine($"  --logging.steps-per-log 500 ^");
                batchFileBuilder.AppendLine($"  --max-num-iterations 30000 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.hidden-dim 128 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.hidden-dim-color 128 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.num-levels 16 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.max-res 2048 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.log2-hashmap-size 19 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.num-proposal-samples-per-ray 256 96 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.num-nerf-samples-per-ray 96 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-warmup 5000 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-weights-anneal-max-num-iters 5000 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.use-proposal-weight-anneal True ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.appearance-embed-dim 32 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-net-args-list.0.hidden-dim 16 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-net-args-list.0.log2-hashmap-size 17 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-net-args-list.0.num-levels 5 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-net-args-list.0.max-res 128 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-net-args-list.1.hidden-dim 16 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-net-args-list.1.log2-hashmap-size 17 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-net-args-list.1.num-levels 5 ^");
                batchFileBuilder.AppendLine($"  --pipeline.model.proposal-net-args-list.1.max-res 256 ^");
                batchFileBuilder.AppendLine($"  --pipeline.datamanager.train-num-rays-per-batch 8192 ^");
                batchFileBuilder.AppendLine($"  --pipeline.datamanager.eval-num-rays-per-batch 8192 ^");
                batchFileBuilder.AppendLine($"  --optimizers.fields.optimizer.lr 0.01 ^");
                batchFileBuilder.AppendLine($"  --optimizers.fields.scheduler.max-steps 30000");
                batchFileBuilder.AppendLine("if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%");
                batchFileBuilder.AppendLine();

                // Configure PyTorch memory management
                batchFileBuilder.AppendLine("REM Configure PyTorch memory management");
                batchFileBuilder.AppendLine("set PYTORCH_CUDA_ALLOC_CONF=max_split_size_mb:128");
                batchFileBuilder.AppendLine();

                batchFileBuilder.AppendLine("echo Exporting high-quality TSDF mesh...");
                batchFileBuilder.AppendLine("REM Configure PyTorch memory management");
                batchFileBuilder.AppendLine("set PYTORCH_CUDA_ALLOC_CONF=max_split_size_mb:128");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine($"call ns-export tsdf ^");
                batchFileBuilder.AppendLine($"  --load-config \"{dataPath}\\output\\SfM\\nerfacto\\{timeStamp}\\config.yml\" ^");
                batchFileBuilder.AppendLine($"  --output-dir \"{dataPath}\\output\\obj\" ^");
                batchFileBuilder.AppendLine($"  --num-pixels-per-side 1024 ^");
                batchFileBuilder.AppendLine($"  --batch-size 2 ^");
                batchFileBuilder.AppendLine($"  --px-per-uv-triangle 4 ^");
                batchFileBuilder.AppendLine($"  --unwrap-method xatlas");
                batchFileBuilder.AppendLine("if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine("echo Workflow completed successfully at %DATE% %TIME%");
                batchFileBuilder.AppendLine();
                batchFileBuilder.AppendLine("exit /b 0");

                var batchFilePath = Path.Combine(dataPath, $"nerfstudio_{timeStamp}.bat");
                await File.WriteAllTextAsync(batchFilePath, batchFileBuilder.ToString());

                await LogMessage($"Batch file: {batchFilePath}");

                var exitCode = ExecuteProcess("cmd.exe", $"/c \"{batchFilePath}\"");
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"NeRFStudio batch file failed with exit code {exitCode}.");
                }

                await LogMessage($"*** {Name} processing completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** OBJ2BIN started...");

                var exePath = Configuration["ToolPaths:OBJ2BIN"] + @"\obj2bin.exe";
                var args = $"-convert {dataPath}\\output\\obj {dataPath}\\output\\obj";

                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"OBJ2BIN failed with exit code {exitCode}.");
                }

                await LogMessage($"*** OBJ2BIN completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                UpdateLibrary(model, $"{dataPath}\\output");
            }

            await LogMessage($"*** {Name} Workflow completed after {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");

            return true;
        }

        public override string Name => NAME;
    }
}