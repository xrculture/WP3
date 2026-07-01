using Microsoft.Extensions.Configuration;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using XRCulture3DReconstruction.Services;

namespace XRCulture3DReconstruction.Workflows
{
    public class openMVS : _Workflow
    {
        public openMVS(IConfiguration configuration, Serilog.ILogger logger, ISignalRLoggerService signalRLogger, string groupName, string taskId, Dictionary<string, string>? options)
            : base(configuration, logger, signalRLogger, groupName, taskId, options)
        {
        }

        public async Task<bool> Execute(string inputDir)
        {
            return await MultiViewStereo(inputDir);
        }

        private async Task<bool> MultiViewStereo(string inputDir)
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            await LogMessage("openMVS: Create Multi View Stereo reconstruction started...");

            var quality = Options?.GetValueOrDefault("quality", "High") ?? "Ultra";

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var toolPaths = isLinuxPlatform ? "ToolPathsLinux" : "ToolPaths";
            var sep = Path.DirectorySeparatorChar;

            {
                await LogMessage("*** Importing 3D reconstruction from openMVG started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVG"] + (isLinuxPlatform ? "/openMVG_main_openMVG2openMVS" : @"\openMVG_main_openMVG2openMVS.exe");
                var args = $"--sfmdata {inputDir}{sep}reconstruction{sep}sfm_data.bin --outfile {inputDir}{sep}model.mvs --outdir {inputDir}{sep}undistored";

                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"openMVG_main_openMVG2openMVS failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Importing 3D reconstruction from openMVG completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** Creating Density Point Cloud started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVS"] + (isLinuxPlatform ? "/DensifyPointCloud" : @"\DensifyPointCloud.exe");
                var args = $"--working-folder {inputDir} --input-file {inputDir}{sep}model.mvs --resolution-level 0 --min-resolution 640 --max-resolution 3200 --number-views 8 --number-views-fuse 6 --estimate-colors 1 --estimate-normals 1";

                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"DensifyPointCloud failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Creating Density Point Cloud completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** Reconstructing Mesh started...");

                var exePath = Configuration[$"{toolPaths}:OpenMVS"] + (isLinuxPlatform ? "/ReconstructMesh" : @"\ReconstructMesh.exe");
                var args = $"--working-folder {inputDir} --archive-type 2 --input-file {inputDir}{sep}model_dense.mvs --thickness-factor 1 --quality-factor 1 --decimate 0.5";

                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"ReconstructMesh failed with exit code {exitCode}.");
                }

                await LogMessage($"*** Reconstructing Mesh completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            if (quality.Equals("ULTRA", StringComparison.OrdinalIgnoreCase))
            {
                {
                    await LogMessage("*** Refining Mesh started...");

                    var exePath = Configuration[$"{toolPaths}:OpenMVS"] + (isLinuxPlatform ? "/RefineMesh" : @"\RefineMesh.exe");
                    var args = $"--working-folder {inputDir} --resolution-level 0 --input-file {inputDir}{sep}model_dense_mesh.mvs";

                    var exitCode = ExecuteProcess(exePath, args);
                    if (exitCode != 0)
                    {
                        await LogMessage($"Process failed with exit code {exitCode}.", true);
                        throw new Exception($"RefineMesh failed with exit code {exitCode}.");
                    }

                    await LogMessage($"*** Refining Mesh completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
                }

                {
                    await LogMessage("*** Texturing Mesh started...");

                    var exePath = Configuration[$"{toolPaths}:OpenMVS"] + (isLinuxPlatform ? "/TextureMesh" : @"\TextureMesh.exe");
                    var args = $"--working-folder {inputDir} --export-type=obj --output-file {inputDir}{sep}obj{sep}model.obj --input-file {inputDir}{sep}model_dense_mesh_refine.mvs --resolution-level 0 --min-resolution 1024 --outlier-threshold 0.6 --cost-smoothness-ratio 0.1 --patch-packing-heuristic 3";

                    var exitCode = ExecuteProcess(exePath, args);
                    if (exitCode != 0)
                    {
                        await LogMessage($"Process failed with exit code {exitCode}.", true);
                        throw new Exception($"TextureMesh failed with exit code {exitCode}.");
                    }

                    await LogMessage($"*** Texturing Mesh completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
                }
            } // ULTRA
            else
            {
                {
                    await LogMessage("*** Texturing Mesh started...");

                    var exePath = Configuration[$"{toolPaths}:OpenMVS"] + (isLinuxPlatform ? "/TextureMesh" : @"\TextureMesh.exe");
                    var args = $"--working-folder {inputDir} --export-type=obj --output-file {inputDir}{sep}obj{sep}model.obj --input-file {inputDir}{sep}model_dense_mesh.mvs --resolution-level 0 --min-resolution 1024 --outlier-threshold 0.6 --cost-smoothness-ratio 0.1 --patch-packing-heuristic 3";

                    var exitCode = ExecuteProcess(exePath, args);
                    if (exitCode != 0)
                    {
                        await LogMessage($"Process failed with exit code {exitCode}.", true);
                        throw new Exception($"TextureMesh failed with exit code {exitCode}.");
                    }

                    await LogMessage($"*** Texturing Mesh completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
                }
            }

            {
                await LogMessage("*** MeshLab Quadric Edge Collapse Decimation with texture preservation started...");

                // XML request for MeshLab Server
                string applyFilterRequest =
@"<ApplyFilterRequest>
    <Name>%FILTER%</Name>
    <Parameters>
        <InputMesh>%INPUT_MESH%</InputMesh>
        <OutputMesh>%OUTPUT_MESH%</OutputMesh>
    </Parameters>
</ApplyFilterRequest>";
                applyFilterRequest = applyFilterRequest.Replace("%FILTER%", "align_center_and_decimate");
                applyFilterRequest = applyFilterRequest.Replace("%INPUT_MESH%", "model.obj");
                applyFilterRequest = applyFilterRequest.Replace("%OUTPUT_MESH%", "model.obj");

                // Archive obj directory
                string objDir = Path.Combine(inputDir, "obj");
                await CreateZipArchive(objDir, inputDir, "model.zip");

                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromMinutes(60);

                        var url = Configuration[$"{(isLinuxPlatform ? "ServicesLinux" : "Services")}:MeshLabServer"] + "Filters?handler=Apply2";
                        using (var form = new MultipartFormDataContent())
                        {
                            // Add XML request as a form part
                            form.Add(new StringContent(applyFilterRequest, Encoding.UTF8, "application/xml"), "request", "request.xml");

                            // Add the zip file as a form part
                            using (var fileStream = File.OpenRead(Path.Combine(inputDir, "model.zip")))
                            {
                                if (fileStream == null || fileStream.Length == 0)
                                {
                                    throw new Exception("File stream is null or empty. Please check the file path.");
                                }

                                var fileContent = new StreamContent(fileStream);
                                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
                                form.Add(fileContent, "file", "model.zip");

                                var response = await RetryPostAsync(client, url, form);
                                if (response == null)
                                {
                                    throw new Exception("POST request failed.");
                                }

                                string responseString = await response.Content.ReadAsStringAsync();

                                var xmlDoc = new XmlDocument();
                                xmlDoc.LoadXml(responseString);

                                var status = xmlDoc.SelectSingleNode("//Status")?.InnerText;
                                if (status?.Trim() != "200")
                                {
                                    throw new Exception($"MeshLab Server returned error: {status}");
                                }

                                var resultId = xmlDoc.SelectSingleNode("//Parameters/ResultId")?.InnerText;
                                if (string.IsNullOrEmpty(resultId))
                                {
                                    throw new Exception("MeshLab Server did not return a 'ResultId'.");
                                }

                                url = Configuration[$"{(isLinuxPlatform ? "ServicesLinux" : "Services")}:MeshLabServer"] + $"Filters?handler=ResultContents&resultId={Uri.EscapeDataString(resultId)}";
                                var resultResponse = await client.GetAsync(url);
                                resultResponse.EnsureSuccessStatusCode();
                                var dirJson = await resultResponse.Content.ReadAsStringAsync();

                                var files = System.Text.Json.JsonSerializer.Deserialize<List<string>>(dirJson);
                                if (files == null || files.Count == 0)
                                {
                                    throw new Exception("MeshLab Server didn't return any result files.");
                                }

                                // Clean up obj folder                            
                                if (Directory.Exists(objDir))
                                {
                                    foreach (var file in Directory.GetFiles(objDir))
                                    {
                                        File.Delete(file);
                                    }
                                }

                                // Get the result files
                                foreach (var file in files)
                                {
                                    // Retrieve
                                    var fileUrl = Configuration[$"{(isLinuxPlatform ? "ServicesLinux" : "Services")}:MeshLabServer"] + $"Filters?handler=ResultFile&resultId={Uri.EscapeDataString(resultId)}&file={Uri.EscapeDataString(file)}";
                                    var fileResponse = await client.GetAsync(fileUrl);
                                    fileResponse.EnsureSuccessStatusCode();

                                    // Save
                                    var fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();
                                    await File.WriteAllBytesAsync(Path.Combine(objDir, file), fileBytes);
                                }

                                url = Configuration[$"{(isLinuxPlatform ? "ServicesLinux" : "Services")}:MeshLabServer"] + $"Filters?handler=Result&resultId={Uri.EscapeDataString(resultId)}";
                                var deleteResponse = await client.DeleteAsync(url);
                                deleteResponse.EnsureSuccessStatusCode();
                            }
                        }
                    }
                }

                await LogMessage($"*** MeshLab Quadric Edge Collapse Decimation with texture preservation completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            {
                await LogMessage("*** OBJ2BIN started...");

                var exePath = Configuration[$"{toolPaths}:OBJ2BIN"] + (isLinuxPlatform ? "/obj2bin" : @"\obj2bin.exe");
                var args = $"-convert {inputDir}{sep}obj {inputDir}{sep}obj";
                if (isLinuxPlatform)
                {
                    args = $"{inputDir}{sep}obj{sep}model.obj {inputDir}{sep}obj{sep}model.obj.bin";
                }

                var exitCode = ExecuteProcess(exePath, args);
                if (exitCode != 0)
                {
                    await LogMessage($"Process failed with exit code {exitCode}.", true);
                    throw new Exception($"OBJ2BIN failed with exit code {exitCode}.");
                }

                await LogMessage($"*** OBJ2BIN completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");
            }

            await LogMessage($"openMVS: Create Multi View Stereo reconstruction completed successfully in {stopWatch.Elapsed:hh\\:mm\\:ss\\.fff}.");

            return true;
        }

        public override string Name => "openMVS";
    }
}
