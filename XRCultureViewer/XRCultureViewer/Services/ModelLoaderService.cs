using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml;

namespace XRCultureViewer.Services
{
    public class ModelLoaderService : IModelLoaderService
    {
        private readonly ILogger<ModelLoaderService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IThumbnailService _thumbnailService;

        public ModelLoaderService(
            ILogger<ModelLoaderService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IThumbnailService thumbnailService)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _thumbnailService = thumbnailService;
        }

        public async Task<IActionResult> LoadModelXMLAsync(XmlDocument xmlDoc, string serviceRootUrl)
        {
            var sessionToken = xmlDoc.SelectSingleNode("/ModelLoadingRequest/SessionToken")?.InnerText;
            if (string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogError("Bad request: 'SessionToken'.");
                return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'SessionToken'.") };
            }

            //#todo: Session Token support
            if (sessionToken != "e3be7cc2-3a7e-45e6-9a88-bd364e6de740")
            {
                _logger.LogError("Bad request: 'SessionToken'.");
                return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'SessionToken'.") };
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var modelsDir = _configuration[$"{FileStorage}:ModelsDir"];
            if (string.IsNullOrEmpty(modelsDir))
            {
                _logger.LogError("Models path is not configured.");
                return new ContentResult { Content = HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Models path is not configured."), ContentType = "application/xml" };
            }

            // ************************************************************************************
            // oEmbed
            var oEmbedNode = xmlDoc.SelectSingleNode("/ModelLoadingRequest/oEmbed");
            var oEmbed = oEmbedNode != null && oEmbedNode.InnerText.Equals("true", StringComparison.OrdinalIgnoreCase);

            // ************************************************************************************
            // LocalSource
            var base64Content = xmlDoc.SelectSingleNode("/ModelLoadingRequest/Source/LocalSource")?.InnerText;
            if (!string.IsNullOrEmpty(base64Content))
            {
                var modelName = xmlDoc.SelectSingleNode("/ModelLoadingRequest/Source/LocalSource/@filename")?.Value;
                if (string.IsNullOrEmpty(modelName))
                {
                    _logger.LogError("Bad request: 'filename'.");
                    return new ContentResult { Content = HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Bad request: 'filename'.") };
                }

                var fileExtension = xmlDoc.SelectSingleNode("/ModelLoadingRequest/Source/LocalSource/@extension")?.Value;
                if (string.IsNullOrEmpty(fileExtension))
                {
                    _logger.LogError("Bad request: 'extension'.");
                    return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'extension'.") };
                }

                if (!fileExtension.StartsWith("."))
                    fileExtension = "." + fileExtension;

                var fileDimension = xmlDoc.SelectSingleNode("/ModelLoadingRequest/Source/LocalSource/@dimension")?.Value;
                if (string.IsNullOrEmpty(fileDimension))
                {
                    _logger.LogError("Bad request: 'dimension'.");
                    return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'dimension'.") };
                }

                var resultId = Guid.NewGuid().ToString();
                _logger.LogInformation("Generated new model ID: {ResultId}", resultId);

                var modelPath = Path.Combine(modelsDir, $"{resultId}{fileExtension}");
                try
                {
                    byte[] fileBytes = Convert.FromBase64String(base64Content);
                    using (var fs = System.IO.File.Create(modelPath))
                    {
                        await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                    }

                    _logger.LogInformation("Successfully saved file from base64 content to {FilePath}", modelPath);
                }
                catch (FormatException ex)
                {
                    _logger.LogError(ex, "Invalid base64 string format");
                    return new ContentResult { Content = HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Invalid base64 content format") };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving file");
                    return new ContentResult { Content = HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", $"Error saving file: {ex.Message}") };
                }

                WriteMetadataXml(modelsDir, resultId, fileExtension, modelName, modelName);
                _thumbnailService.Enqueue($"{resultId}{fileExtension}");

                var viewer = fileExtension.Equals(".splat", StringComparison.OrdinalIgnoreCase) || 
                    fileExtension.Equals(".ply", StringComparison.OrdinalIgnoreCase) ? 
                        "GaussianSplattingViewer" : "Viewer";
                var viewerUrl = $"{serviceRootUrl}{viewer}?model={resultId}{fileExtension}";
                var endPointUrl = oEmbed ? $"{serviceRootUrl}oembed?url={Uri.EscapeDataString(viewerUrl)}&format=xml" : viewerUrl;
                var thumbnailUrl = $"{serviceRootUrl}Storage?handler=Thumbnail&id={resultId}.jpg";
                var response = HTTPResponse.ModelLoadingResponseSuccessXML
                    .Replace("%SESSION_TOKEN%", System.Security.SecurityElement.Escape(sessionToken))
                    .Replace("%SIZE%", System.Security.SecurityElement.Escape(fileDimension))
                    .Replace("%ENDPOINT%", System.Security.SecurityElement.Escape(endPointUrl))
                    .Replace("%THUMBNAIL%", System.Security.SecurityElement.Escape(thumbnailUrl));
                _logger.LogInformation("Model uploaded and saved successfully with ID: {ResultId}", resultId);
                return new ContentResult { Content = response, ContentType = "application/xml" };
            } // LocalSource

            // ************************************************************************************
            // UrlSource
            var url = xmlDoc.SelectSingleNode("/ModelLoadingRequest/Source/UrlSource/Url")?.InnerText;
            if (!string.IsNullOrEmpty(url))
            {
                url = Uri.UnescapeDataString(url);

                var fileExtension = xmlDoc.SelectSingleNode("/ModelLoadingRequest/Source/UrlSource/FileExtension")?.InnerText;
                if (string.IsNullOrEmpty(fileExtension))
                {
                    _logger.LogError("Bad request: 'FileExtension'.");
                    return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'FileExtension'.") };
                }

                var fileDimension = xmlDoc.SelectSingleNode("/ModelLoadingRequest/Source/UrlSource/FileDimension")?.InnerText;
                if (string.IsNullOrEmpty(fileDimension))
                {
                    _logger.LogError("Bad request: 'FileDimension'.");
                    return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'FileDimension'.") };
                }

                var resultId = Guid.NewGuid().ToString();
                _logger.LogInformation("Generated new model ID: {ResultId} for URL: {Url}", resultId, url);

                var modelPath = Path.Combine(modelsDir, $"{resultId}{fileExtension}");
                try
                {
                    byte[] fileBytes = await DownloadModelFromUrlAsync(url);

                    using (var fs = System.IO.File.Create(modelPath))
                    {
                        await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                    }

                    _logger.LogInformation("Successfully downloaded and saved file from URL {Url} to {FilePath}", url, modelPath);

                    WriteMetadataXml(modelsDir, resultId, fileExtension, Path.GetFileName(url), $"Downloaded from {url}");
                    _thumbnailService.Enqueue($"{resultId}{fileExtension}");

                    var viewer = fileExtension.Equals(".splat", StringComparison.OrdinalIgnoreCase) || 
                        fileExtension.Equals(".ply", StringComparison.OrdinalIgnoreCase) ? 
                            "GaussianSplattingViewer" : "Viewer";
                    var viewerUrl = $"{serviceRootUrl}{viewer}?model={resultId}{fileExtension}";
                    var endPointUrl = oEmbed ? $"{serviceRootUrl}oembed?url={Uri.EscapeDataString(viewerUrl)}&format=xml" : viewerUrl;
                    var thumbnailUrl = $"{serviceRootUrl}Storage?handler=Thumbnail&id={resultId}.jpg";
                    var response = HTTPResponse.ModelLoadingResponseSuccessXML
                        .Replace("%SESSION_TOKEN%", System.Security.SecurityElement.Escape(sessionToken))
                        .Replace("%SIZE%", System.Security.SecurityElement.Escape(fileDimension))
                        .Replace("%ENDPOINT%", System.Security.SecurityElement.Escape(endPointUrl))
                        .Replace("%THUMBNAIL%", System.Security.SecurityElement.Escape(thumbnailUrl));

                    _logger.LogInformation("Model downloaded and saved successfully with ID: {ResultId}", resultId);
                    return new ContentResult { Content = response, ContentType = "application/xml" };
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "Invalid URL: {Url}", url);
                    return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestXML.Replace("%MESSAGE%", $"Invalid URL: {ex.Message}") };
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogError(ex, "Download failed for URL: {Url}", url);
                    return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestXML.Replace("%MESSAGE%", $"Download failed: {ex.Message}") };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing URL download: {Url}", url);
                    return new ContentResult { Content = HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", $"Error processing download: {ex.Message}") };
                }
            } // UrlSource

            _logger.LogError("Bad request: 'Unsupported content'.");
            return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestXML.Replace("%MESSAGE%", "Bad request: 'Unsupported content'.") };
        }

        public async Task<IActionResult> LoadModelJSONAsync(dynamic jsonDoc, string serviceRootUrl)
        {
            try
            {
                // Cast dynamic properties to string (CS1973)
                string? sessionToken = jsonDoc.SessionToken?.ToString();
                if (string.IsNullOrEmpty(sessionToken))
                {
                    _logger.LogError("Bad request: 'SessionToken'.");
                    return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'SessionToken'."), ContentType = "application/json" };
                }

                //#todo: Session Token support
                if (sessionToken != "e3be7cc2-3a7e-45e6-9a88-bd364e6de740")
                {
                    _logger.LogError("Bad request: 'SessionToken'.");
                    return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'SessionToken'."), ContentType = "application/json" };
                }

                var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
                var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

                var modelsDir = _configuration[$"{FileStorage}:ModelsDir"];
                if (string.IsNullOrEmpty(modelsDir))
                {
                    _logger.LogError("Models path is not configured.");
                    return new ContentResult { Content = HTTPResponse.ServerErrorJSON.Replace("%MESSAGE%", "Models path is not configured."), ContentType = "application/json" };
                }

                // ************************************************************************************
                // oEmbed                
                var oEmbedValue = jsonDoc.oEmbed;
                var oEmbed = oEmbedValue != null && oEmbedValue?.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);

                // ************************************************************************************
                // LocalSource
                var localSource = jsonDoc.Source?.LocalSource;
                if (localSource != null)
                {
                    string? base64Content = localSource.FileContent?.ToString();
                    if (!string.IsNullOrEmpty(base64Content))
                    {
                        string? modelName = localSource.FileName?.ToString();
                        if (string.IsNullOrEmpty(modelName))
                        {
                            _logger.LogError("Bad request: 'FileName'.");
                            return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileName'."), ContentType = "application/json" };
                        }

                        string? fileExtension = localSource.FileExtension?.ToString();
                        if (string.IsNullOrEmpty(fileExtension))
                        {
                            _logger.LogError("Bad request: 'FileExtension'.");
                            return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileExtension'."), ContentType = "application/json" };
                        }

                        string? fileDimension = localSource.FileDimension?.ToString();
                        if (string.IsNullOrEmpty(fileDimension))
                        {
                            _logger.LogError("Bad request: 'FileDimension'.");
                            return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileDimension'."), ContentType = "application/json" };
                        }

                        var resultId = Guid.NewGuid().ToString();
                        _logger.LogInformation("Generated new model ID: {ResultId}", resultId);

                        var modelPath = Path.Combine(modelsDir, $"{resultId}{fileExtension}");
                        try
                        {
                            byte[] fileBytes = Convert.FromBase64String(base64Content);
                            using (var fs = System.IO.File.Create(modelPath))
                            {
                                await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                            }

                            _logger.LogInformation("Successfully saved file from base64 content to {FilePath}", modelPath);
                        }
                        catch (FormatException ex)
                        {
                            _logger.LogError(ex, "Invalid base64 string format");
                            return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", "Invalid base64 content format"), ContentType = "application/json" };
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error saving file");
                            return new ContentResult { Content = HTTPResponse.ServerErrorJSON.Replace("%MESSAGE%", $"Error saving file: {ex.Message}"), ContentType = "application/json" };
                        }

                        WriteMetadataXml(modelsDir, resultId, fileExtension, modelName, modelName);
                        _thumbnailService.Enqueue($"{resultId}{fileExtension}");

                        var viewer = fileExtension.Equals(".splat", StringComparison.OrdinalIgnoreCase) || 
                                     fileExtension.Equals(".ply", StringComparison.OrdinalIgnoreCase) ? 
                                        "GaussianSplattingViewer" : "Viewer";
                        var viewerUrl = $"{serviceRootUrl}{viewer}?model={resultId}{fileExtension}";
                        var endPointUrl = oEmbed ? $"{serviceRootUrl}oembed?url={Uri.EscapeDataString(viewerUrl)}&format=xml" : viewerUrl;
                        var thumbnailUrl = $"{serviceRootUrl}Storage?handler=Thumbnail&id={resultId}.jpg";
                        var response = HTTPResponse.ModelLoadingResponseSuccessJSON
                            .Replace("%SESSION_TOKEN%", System.Security.SecurityElement.Escape(sessionToken))
                            .Replace("%SIZE%", System.Security.SecurityElement.Escape(fileDimension))
                            .Replace("%ENDPOINT%", System.Security.SecurityElement.Escape(endPointUrl))
                            .Replace("%THUMBNAIL%", System.Security.SecurityElement.Escape(thumbnailUrl));

                        _logger.LogInformation("Model uploaded and saved successfully with ID: {ResultId}", resultId);
                        return new ContentResult { Content = response, ContentType = "application/json" };
                    }
                } // LocalSource

                // ************************************************************************************
                // UrlSource
                var urlSource = jsonDoc.Source?.UrlSource;
                if (urlSource != null)
                {
                    string? url = urlSource.Url?.ToString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        string? fileExtension = urlSource.FileExtension?.ToString();
                        if (string.IsNullOrEmpty(fileExtension))
                        {
                            _logger.LogError("Bad request: 'FileExtension'.");
                            return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileExtension'."), ContentType = "application/json" };
                        }

                        string? fileDimension = urlSource.FileDimension?.ToString();
                        if (string.IsNullOrEmpty(fileDimension))
                        {
                            _logger.LogError("Bad request: 'FileDimension'.");
                            return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'FileDimension'."), ContentType = "application/json" };
                        }

                        var resultId = Guid.NewGuid().ToString();
                        _logger.LogInformation("Generated new model ID: {ResultId} for URL: {Url}", resultId, url);

                        var modelPath = Path.Combine(modelsDir, $"{resultId}{fileExtension}");
                        try
                        {
                            byte[] fileBytes = await DownloadModelFromUrlAsync(url);

                            using (var fs = System.IO.File.Create(modelPath))
                            {
                                await fs.WriteAsync(fileBytes, 0, fileBytes.Length);
                            }

                            _logger.LogInformation("Successfully downloaded and saved file from URL {Url} to {FilePath}", url, modelPath);

                            WriteMetadataXml(modelsDir, resultId, fileExtension, Path.GetFileName(url), $"Downloaded from {url}");
                            _thumbnailService.Enqueue($"{resultId}{fileExtension}");

                            var viewer = fileExtension.Equals(".splat", StringComparison.OrdinalIgnoreCase) || 
                                         fileExtension.Equals(".ply", StringComparison.OrdinalIgnoreCase) ?
                                        "GaussianSplattingViewer" : "Viewer";
                            var viewerUrl = $"{serviceRootUrl}{viewer}?model={resultId}{fileExtension}";
                            var endPointUrl = oEmbed ? $"{serviceRootUrl}oembed?url={Uri.EscapeDataString(viewerUrl)}&format=xml" : viewerUrl;
                            var thumbnailUrl = $"{serviceRootUrl}Storage?handler=Thumbnail&id={resultId}.jpg";
                            var response = HTTPResponse.ModelLoadingResponseSuccessJSON
                                .Replace("%SESSION_TOKEN%", System.Security.SecurityElement.Escape(sessionToken))
                                .Replace("%SIZE%", System.Security.SecurityElement.Escape(fileDimension))
                                .Replace("%ENDPOINT%", System.Security.SecurityElement.Escape(endPointUrl))
                                .Replace("%THUMBNAIL%", System.Security.SecurityElement.Escape(thumbnailUrl));

                            _logger.LogInformation("Model downloaded and saved successfully with ID: {ResultId}", resultId);
                            return new ContentResult { Content = response, ContentType = "application/json" };
                        }
                        catch (ArgumentException ex)
                        {
                            _logger.LogError(ex, "Invalid URL: {Url}", url);
                            return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", $"Invalid URL: {ex.Message}"), ContentType = "application/json" };
                        }
                        catch (InvalidOperationException ex)
                        {
                            _logger.LogError(ex, "Download failed for URL: {Url}", url);
                            return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", $"Download failed: {ex.Message}"), ContentType = "application/json" };
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing URL download: {Url}", url);
                            return new ContentResult { Content = HTTPResponse.ServerErrorJSON.Replace("%MESSAGE%", $"Error processing download: {ex.Message}"), ContentType = "application/json" };
                        }
                    }
                } // UrlSource

                _logger.LogError("Bad request: 'Unsupported content'.");
                return new ContentResult { Content = HTTPResponse.ModelLoadingResponseBadRequestJSON.Replace("%MESSAGE%", "Bad request: 'Unsupported content'."), ContentType = "application/json" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in LoadModelJSONAsync");
                return new ContentResult { Content = HTTPResponse.ServerErrorJSON.Replace("%MESSAGE%", $"Unexpected error: {ex.Message}"), ContentType = "application/json" };
            }
        }

        private async Task<byte[]> DownloadModelFromUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException("Invalid URL format", nameof(url));

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("Only HTTP and HTTPS URLs are supported", nameof(url));

            using var httpClient = _httpClientFactory.CreateClient();
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

        private void WriteMetadataXml(string modelsDir, string resultId, string fileExtension, string name, string description)
        {
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<Model>");
            xml.AppendLine($"\t<Id>{resultId}</Id>");
            xml.AppendLine($"\t<Extension>{fileExtension}</Extension>");
            xml.AppendLine($"\t<Name>{name}</Name>");
            xml.AppendLine($"\t<Description>{description}</Description>");
            xml.AppendLine($"\t<TimeStamp>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</TimeStamp>");
            xml.AppendLine("</Model>");
            System.IO.File.WriteAllText(Path.Combine(modelsDir, $"{resultId}.xml"), xml.ToString());
        }
    }
}