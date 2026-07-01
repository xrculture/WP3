using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace XRCultureServices.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class ConvertModel : PageModel
    {
        private readonly ILogger<ConvertModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public ConvertModel(ILogger<ConvertModel> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        [DllImport("STEP2glTF.dll")] // Windows DLL
        //[DllImport("libstep2gltf.so")] // Linux shared library (Docker)    
        private static extern void ConvertSTEP2glTF(string inputPath, string outputPath);

        public void OnGet()
        {
        }

        /*
         * <?xml version=""1.0"" encoding=""UTF-8""?>
        <ConversionRequest>
            <Source>
              <LocalSource>
                <Name>%NAME%</Name>
                <Description>3D Model file</Description>
                <FileContent dimension=""%SIZE%"" extension=""%EXTENSION%"">%BASE64_CONTENT%</FileContent>
              </LocalSource>
            </Source>
        </ConversionRequest>
        */
        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Received request.");

            bool bJSONContentType = Request.ContentType?.StartsWith("application/json") == true;

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrEmpty(body))
            {
                _logger.LogInformation("Received empty request.");
                return Content(bJSONContentType ?
                    HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Received empty request.") :
                    HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Received empty request."));
            }

            dynamic? jsonRequest = JsonConvert.DeserializeObject(body);
            if (jsonRequest == null)
            {
                _logger.LogInformation("Received empty request.");
                return Content(bJSONContentType ?
                    HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Received empty request.") :
                    HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Received empty request."));
            }

            _logger.LogInformation($"****** Request ******\n{body}");

            if (bJSONContentType)
            {
                return await ConvertModelJSON(jsonRequest);
            } // application/json
            else
            {
                XmlDocument viewModelRequestXml = new();
                try
                {
                    var xmlBody = JsonConvert.DeserializeObject<string>(body);
                    viewModelRequestXml.LoadXml(xmlBody);
                }
                catch (XmlException ex)
                {
                    _logger.LogError(ex, $"Invalid XML format: {ex.Message}");
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", $"Invalid XML format: {ex.Message}"));
                }

                return await ConvertModelXML(viewModelRequestXml);
            } // application/xml            
        }

        private async Task<IActionResult> ConvertModelXML(XmlDocument xmlDoc)
        {
            byte[] fileBytes;

            var sessionToken = xmlDoc.SelectSingleNode("/ConversionRequest/SessionToken")?.InnerText;
            if (string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogError("Bad request: 'SessionToken'.");
                return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'SessionToken'."));
            }

            //#todo: Session Token support
            if (sessionToken != "cvt-8b1d5d1d-4d11-4f34-9b69-0a3d91f1b7f1")
            {
                _logger.LogError("Bad request: 'SessionToken'.");
                return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'SessionToken'."));
            }

            var isLinuxPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var modelsDir = _configuration[$"{FileStorage}:ModelsDir"];
            if (string.IsNullOrEmpty(modelsDir))
            {
                _logger.LogError("Models path is not configured.");
                return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Models path is not configured."), "application/xml");
            }

            var originFormatExtension = xmlDoc.SelectSingleNode("/ConversionRequest/OriginFormat/@extension")?.Value;
            if (string.IsNullOrEmpty(originFormatExtension))
            {
                _logger.LogError("Bad request: 'OriginFormat'.");
                return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'OriginFormat'."));
            }

            var destinationFormatExtension = xmlDoc.SelectSingleNode("/ConversionRequest/DestinationFormat/@extension")?.Value;
            if (string.IsNullOrEmpty(destinationFormatExtension))
            {
                _logger.LogError("Bad request: 'DestinationFormat'.");
                return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'DestinationFormat'."));
            }

            // ************************************************************************************
            // LocalSource
            var base64Content = xmlDoc.SelectSingleNode("/ConversionRequest/Source/LocalSource")?.InnerText;
            if (!string.IsNullOrEmpty(base64Content))
            {
                var fileName = xmlDoc.SelectSingleNode("/ConversionRequest/Source/LocalSource/@filename")?.Value;
                if (string.IsNullOrEmpty(fileName))
                {
                    _logger.LogError("Bad request: 'filename'.");
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Bad request: 'filename'."));
                }

                var fileExtension = xmlDoc.SelectSingleNode("/ConversionRequest/Source/LocalSource/@extension")?.Value;
                if (string.IsNullOrEmpty(fileExtension))
                {
                    _logger.LogError("Bad request: 'extension'.");
                    return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'extension'."));
                }

                if (!fileExtension.StartsWith("."))
                {
                    fileExtension = "." + fileExtension;
                }

                var fileDimension = xmlDoc.SelectSingleNode("/ConversionRequest/Source/LocalSource/@dimension")?.Value;
                if (string.IsNullOrEmpty(fileDimension))
                {
                    _logger.LogError("Bad request: 'dimension'.");
                    return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'dimension'."));
                }

                // Save
                var resultId = Guid.NewGuid().ToString();
                _logger.LogInformation("Generated new model ID: {ResultId}", resultId);

                var modelPath = Path.Combine(modelsDir, $"{resultId}{fileExtension}");
                try
                {
                    fileBytes = Convert.FromBase64String(base64Content);
                    using (var fs = System.IO.File.Create(modelPath))
                    {
                        await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                    }

                    _logger.LogInformation("Successfully saved file from base64 content to {FilePath}", modelPath);
                }
                catch (FormatException ex)
                {
                    _logger.LogError(ex, "Invalid base64 string format");
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Invalid base64 content format"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving file");
                    return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", $"Error saving file: {ex.Message}"));
                }

                var outputModelPath = Path.Combine(modelsDir, $"{resultId}{destinationFormatExtension}");
                ConvertSTEP2glTF(modelPath, Path.Combine(modelsDir, outputModelPath));

                var fileInfo = new FileInfo(outputModelPath);
                fileBytes = System.IO.File.ReadAllBytes(outputModelPath);
                var fileLength = fileBytes.Length;
                base64Content = Convert.ToBase64String(fileBytes);

                try
                {
                    System.IO.File.Delete(modelPath);
                    _logger.LogInformation("Deleted temporary file: {FilePath}", modelPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting temporary file: {FilePath}", modelPath);
                }

                try
                {
                    System.IO.File.Delete(outputModelPath);
                    _logger.LogInformation("Deleted temporary file: {FilePath}", outputModelPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting temporary file: {FilePath}", outputModelPath);
                }

                // Return success response
                var response = HTTPResponse.ConversionResponseSuccessXML.Replace(
                    "%SESSION_TOKEN%", sessionToken).
                    Replace("%SIZE%", fileLength.ToString()).
                    Replace("%EXTENSION%", destinationFormatExtension).
                    Replace("%FILENAME%", $"{fileName}{destinationFormatExtension}").
                    Replace("%BASE64CONTENT%", base64Content);
                return Content(response, "application/xml");
            } // LocalSource

            // ************************************************************************************
            // UrlSource
            var url = xmlDoc.SelectSingleNode("/ConversionRequest/Source/UrlSource/Url")?.InnerText;
            if (!string.IsNullOrEmpty(url))
            {
                url = Uri.UnescapeDataString(url);

                var fileExtension = xmlDoc.SelectSingleNode("/ConversionRequest/Source/UrlSource/FileExtension")?.InnerText;
                if (string.IsNullOrEmpty(fileExtension))
                {
                    _logger.LogError("Bad request: 'FileExtension'.");
                    return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'FileExtension'."));
                }

                var fileDimension = xmlDoc.SelectSingleNode("/ConversionRequest/Source/UrlSource/FileDimension")?.InnerText;
                if (string.IsNullOrEmpty(fileDimension))
                {
                    _logger.LogError("Bad request: 'FileDimension'.");
                    return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'FileDimension'."));
                }

                // Generate new model ID
                var resultId = Guid.NewGuid().ToString();
                _logger.LogInformation("Generated new model ID: {ResultId} for URL: {Url}", resultId, url);

                var modelPath = Path.Combine(modelsDir, $"{resultId}{fileExtension}");
                try
                {
                    // Download the file from URL
                    fileBytes = await DownloadModelFromUrlAsync(url);

                    // Save the downloaded file
                    using (var fs = System.IO.File.Create(modelPath))
                    {
                        await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                    }

                    var outputModelPath = Path.Combine(modelsDir, $"{resultId}{destinationFormatExtension}");
                    ConvertSTEP2glTF(modelPath, Path.Combine(modelsDir, outputModelPath));

                    var fileInfo = new FileInfo(outputModelPath);
                    fileBytes = System.IO.File.ReadAllBytes(outputModelPath);
                    base64Content = Convert.ToBase64String(fileBytes);

                    // Return success response
                    var response = HTTPResponse.ConversionResponseSuccessXML.Replace(
                        "%SESSION_TOKEN%", sessionToken).
                        Replace("%SIZE%", fileInfo.Length.ToString()).
                        Replace("%EXTENSION%", destinationFormatExtension).
                        Replace("%FILENAME%", $"{resultId}{destinationFormatExtension}").
                        Replace("%BASE64CONTENT%", base64Content);
                    return Content(response, "application/xml");
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "Invalid URL: {Url}", url);
                    return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", $"Invalid URL: {ex.Message}"));
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogError(ex, "Download failed for URL: {Url}", url);
                    return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", $"Download failed: {ex.Message}"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing URL download: {Url}", url);
                    return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", $"Error processing download: {ex.Message}"));
                }
            } // UrlSource

            _logger.LogError("Bad request: 'Unsupported content'.");
            return Content(HTTPResponse.ConversionResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'Unsupported content'."));
        }

        private async Task<IActionResult> ConvertModelJSON(dynamic jsonDoc)
        {
            try
            {
                byte[] fileBytes;

                var sessionToken = jsonDoc.SessionToken?.ToString();
                if (string.IsNullOrEmpty(sessionToken))
                {
                    _logger.LogError("Bad request: 'SessionToken'.");
                    return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'SessionToken'."), "application/json");
                }

                //#todo: Session Token support
                if (sessionToken != "cvt-8b1d5d1d-4d11-4f34-9b69-0a3d91f1b7f1")
                {
                    _logger.LogError("Bad request: 'SessionToken'.");
                    return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'SessionToken'."), "application/json");
                }

                var originFormatExtension = jsonDoc.OriginFormat?.extension.ToString();
                if (string.IsNullOrEmpty(originFormatExtension))
                {
                    _logger.LogError("Bad request: 'OriginFormat'.");
                    return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'OriginFormat'."), "application/json");
                }

                var destinationFormatExtension = jsonDoc.DestinationFormat?.extension.ToString();
                if (string.IsNullOrEmpty(destinationFormatExtension))
                {
                    _logger.LogError("Bad request: 'DestinationFormat'.");
                    return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'DestinationFormat'."), "application/json");
                }

                var isLinuxPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
                var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

                var modelsDir = _configuration[$"{FileStorage}:ModelsDir"];
                if (string.IsNullOrEmpty(modelsDir))
                {
                    _logger.LogError("Models path is not configured.");
                    return Content(HTTPResponse.ServerErrorJSON.Replace("%MESSAGE%", "Models path is not configured."), "application/json");
                }

                // ************************************************************************************
                // LocalSource
                var localSource = jsonDoc.Source?.LocalSource;
                if (localSource != null)
                {
                    var base64Content = localSource.FileContent?.ToString();
                    if (!string.IsNullOrEmpty(base64Content))
                    {
                        var fileName = localSource.FileName?.ToString();
                        if (string.IsNullOrEmpty(fileName))
                        {
                            _logger.LogError("Bad request: 'FileName'.");
                            return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileName'."), "application/json");
                        }

                        var fileExtension = localSource.FileExtension?.ToString();
                        if (string.IsNullOrEmpty(fileExtension))
                        {
                            _logger.LogError("Bad request: 'FileExtension'.");
                            return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileExtension'."), "application/json");
                        }

                        var fileDimension = localSource.FileDimension?.ToString();
                        if (string.IsNullOrEmpty(fileDimension))
                        {
                            _logger.LogError("Bad request: 'FileDimension'.");
                            return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileDimension'."), "application/json");
                        }

                        // Save
                        var resultId = Guid.NewGuid().ToString();
                        _logger.LogInformation("Generated new model ID: {ResultId}", resultId);

                        var modelPath = Path.Combine(modelsDir, $"{resultId}{fileExtension}");
                        try
                        {
                            fileBytes = Convert.FromBase64String(base64Content);
                            using (var fs = System.IO.File.Create(modelPath))
                            {
                                await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                            }

                            _logger.LogInformation("Successfully saved file from base64 content to {FilePath}", modelPath);
                        }
                        catch (FormatException ex)
                        {
                            _logger.LogError(ex, "Invalid base64 string format");
                            return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Invalid base64 content format"), "application/json");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error saving file");
                            return Content(HTTPResponse.ServerErrorJSON.Replace("%MESSAGE%", $"Error saving file: {ex.Message}"), "application/json");
                        }

                        var outputModelPath = Path.Combine(modelsDir, $"{resultId}{destinationFormatExtension}");
                        ConvertSTEP2glTF(modelPath, Path.Combine(modelsDir, outputModelPath));

                        var fileInfo = new FileInfo(outputModelPath);
                        fileBytes = System.IO.File.ReadAllBytes(outputModelPath);
                        base64Content = Convert.ToBase64String(fileBytes);

                        // Return success response
                        var response = HTTPResponse.ConversionResponseSuccessJSON.Replace(
                            "%SESSION_TOKEN%", sessionToken).
                            Replace("%SIZE%", fileInfo.Length.ToString()).
                            Replace("%EXTENSION%", destinationFormatExtension).
                            Replace("%FILENAME%", $"{fileName}{destinationFormatExtension}").
                            Replace("%BASE64CONTENT%", base64Content);
                        return Content(response, "application/json");
                    }
                } // LocalSource

                // ************************************************************************************
                // UrlSource
                var urlSource = jsonDoc.Source?.UrlSource;
                if (urlSource != null)
                {
                    var url = urlSource.Url?.ToString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var fileExtension = urlSource.FileExtension?.ToString();
                        if (string.IsNullOrEmpty(fileExtension))
                        {
                            _logger.LogError("Bad request: 'FileExtension'.");
                            return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileExtension'."), "application/json");
                        }

                        var fileDimension = urlSource.FileDimension?.ToString();
                        if (string.IsNullOrEmpty(fileDimension))
                        {
                            _logger.LogError("Bad request: 'FileDimension'.");
                            return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileDimension'."), "application/json");
                        }

                        // Generate new model ID
                        var resultId = Guid.NewGuid().ToString();
                        _logger.LogInformation($"Generated new model ID: {resultId} for URL: {url}");

                        var modelPath = Path.Combine(modelsDir, $"{resultId}{fileExtension}");
                        try
                        {
                            // Download the file from URL
                            fileBytes = await DownloadModelFromUrlAsync(url);

                            // Save the downloaded file
                            using (var fs = System.IO.File.Create(modelPath))
                            {
                                await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                            }

                            _logger.LogInformation($"Successfully downloaded and saved file from URL {url} to {modelPath}");

                            var outputModelPath = Path.Combine(modelsDir, $"{resultId}{destinationFormatExtension}");
                            ConvertSTEP2glTF(modelPath, Path.Combine(modelsDir, outputModelPath));

                            var fileInfo = new FileInfo(outputModelPath);
                            fileBytes = System.IO.File.ReadAllBytes(outputModelPath);
                            string base64Content = Convert.ToBase64String(fileBytes);

                            // Return success response
                            var response = HTTPResponse.ConversionResponseSuccessJSON.Replace(
                                "%SESSION_TOKEN%", sessionToken).
                                Replace("%SIZE%", fileInfo.Length.ToString()).
                                Replace("%EXTENSION%", fileExtension).
                                Replace("%FILENAME%", $"{resultId}{destinationFormatExtension}").
                                Replace("%BASE64CONTENT%", base64Content);
                            return Content(response, "application/json");
                        }
                        catch (ArgumentException ex)
                        {
                            _logger.LogError(ex, $"Invalid URL: {url}");
                            return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", $"Invalid URL: {ex.Message}"), "application/json");
                        }
                        catch (InvalidOperationException ex)
                        {
                            _logger.LogError(ex, $"Download failed for URL: {url}");
                            return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", $"Download failed: {ex.Message}"), "application/json");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing URL download: {url}");
                            return Content(HTTPResponse.ServerErrorJSON.Replace("%MESSAGE%", $"Error processing download: {ex.Message}"), "application/json");
                        }
                    }
                } // UrlSource

                _logger.LogError("Bad request: 'Unsupported content'.");
                return Content(HTTPResponse.ConversionResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'Unsupported content'."), "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ConvertModelJSON");
                return Content(HTTPResponse.ServerErrorJSON.Replace("%MESSAGE%", $"Unexpected error: {ex.Message}"), "application/json");
            }
        }

        private string GetServiceRootUrl()
        {
            var request = HttpContext.Request;
            return $"{request.Scheme}://{request.Host}{request.PathBase}/";
        }

        /// <summary>
        /// Downloads a model file from the specified URL
        /// </summary>
        /// <param name="url">The URL to download from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The downloaded file content as byte array</returns>
        private async Task<byte[]> DownloadModelFromUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException("Invalid URL format", nameof(url));

            // Only allow HTTP and HTTPS schemes for security
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("Only HTTP and HTTPS URLs are supported", nameof(url));

            using var httpClient = _httpClientFactory.CreateClient();

            // Set reasonable timeout and headers
            httpClient.Timeout = TimeSpan.FromMinutes(5);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "XRCultureViewer/1.0");

            _logger.LogInformation("Starting download from URL: {Url}", url);

            try
            {
                using var response = await httpClient.GetAsync(uri, cancellationToken);
                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength;
                if (contentLength.HasValue)
                {
                    _logger.LogInformation("Download content length: {ContentLength} bytes", contentLength.Value);

                    // Check file size limit (100MB)
                    if (contentLength.Value > 100 * 1024 * 1024)
                        throw new InvalidOperationException("File is too large. Maximum size is 100MB.");
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                _logger.LogInformation("Successfully downloaded {ByteCount} bytes from {Url}", fileBytes.Length, url);

                return fileBytes;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while downloading from {Url}", url);
                throw new InvalidOperationException($"Failed to download file from URL: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Timeout while downloading from {Url}", url);
                throw new InvalidOperationException("Download timed out", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while downloading from {Url}", url);
                throw new InvalidOperationException($"Unexpected error during download: {ex.Message}", ex);
            }
        }
    }
}
