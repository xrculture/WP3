using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;
using XRCultureHub.Models;


namespace XRCultureHub.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class RegistryModel : PageModel
    {
        const string authorizationResponseJSON =
@"{
    ""Status"": 202,
    ""SessionToken"": ""%SESSION_TOKEN%"",
    ""ExpiresIn"": 3600,
    ""Message"": ""Service successfully authorized.""
}";

        const string authorizationResponseXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<AuthorizationResponse>
      <Status>202</Status> <!-- ACCEPTED / use standard HTML response status codes https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status -->
      <SessionToken>%SESSION_TOKEN%</SessionToken>
      <ExpiresIn>3600</ExpiresIn> <!-- in seconds -->
      <Message>Service successfully authorized.</Message>
</AuthorizationResponse>";

        const string authorizationResponseErrorJSON =
@"{
    ""Status"": 400,
    ""Message"": ""%MESSAGE%""
}";

        const string authorizationResponseErrorXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<AuthorizationResponse>
      <Status>400</Status> <!-- Bad Request -->
      <Message>%MESSAGE%</Message>
</AuthorizationResponse>";

        const string registrationResponseJSON =
@"{
    ""Status"": 200,
    ""Message"": ""Service successfully registered.""
}";

        const string registrationResponseXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<RegistrationResponse>
      <Status>200</Status>
      <Message>Service successfully registered.</Message>
</RegistrationResponse>";

        const string registrationResponseErrorJSON =
@"{
    ""Status"": 400,
    ""Message"": ""%MESSAGE%""
}";

        const string registrationResponseErrorXML =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<RegistrationResponse>
      <Status>400</Status> <!-- Bad Request -->
      <Message>%MESSAGE%</Message>
</RegistrationResponse>";

        private readonly ILogger<RegistryModel> _logger;
        private readonly IConfiguration _configuration;
        private static readonly ConcurrentDictionary<string, AuthorizationRequest> AuthorizationRequests = new();

        public RegistryModel(ILogger<RegistryModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var sessionToken = Request.Query["SessionToken"];
            var serviceType = Request.Query["ServiceType"].ToString();            
            var accept = Request.Query["Accept"].ToString();
            bool bAcceptXML = !string.IsNullOrEmpty(accept) && 
                (accept.Equals("application/xml", StringComparison.OrdinalIgnoreCase) || 
                    accept.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ||
                        accept.Equals("xml", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogError("Bad request: 'SessionToken'.");
                return Content(bAcceptXML ?
                        HTTPResponse.BadRequestXML.Replace("%MESSAGE%", $"Bad request: 'SessionToken'.") :
                        HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", $"Bad request: 'SessionToken'."),
                        bAcceptXML ? "application/xml" : "application/json");
            }

            if (sessionToken != "e3be7cc2-3a7e-45e6-9a88-bd364e6de740") //#todo
            {
                _logger.LogError("Unauthorized request: invalid 'SessionToken'.");
                return Content(bAcceptXML ?
                        HTTPResponse.UnauthorizedXML.Replace("%MESSAGE%", $"Unauthorized request: invalid 'SessionToken'.") :
                        HTTPResponse.UnauthorizedJSON.Replace("%MESSAGE%", $"Unauthorized request: invalid 'SessionToken'."),
                        bAcceptXML ? "application/xml" : "application/json");
            }

            switch (serviceType)
            {
                case "": // for backward compatibility
                case "Viewer":
                    var fileFormat = Request.Query["FileFormat"].ToString();
                    return await GetViewers(sessionToken, fileFormat, bAcceptXML);
                case "ThumbnailGenerator":
                    fileFormat = Request.Query["FileFormat"].ToString();
                    return await GetThumbnailGenerators(sessionToken, fileFormat, bAcceptXML);
                case "Converter":
                    var originFormat = Request.Query["OriginFormat"].ToString();
                    return await GetConverters(sessionToken, originFormat, bAcceptXML);
                case "MeshFilter":
                    return await GetMeshFilters(sessionToken, bAcceptXML);
                case "Photogrammetry":
                    return await GetPhotogrammetryServices(sessionToken, bAcceptXML);
                case "Repository":
                    return await GetRepositoryServices(sessionToken, bAcceptXML);
                default:
                    _logger.LogError($"Unknown ServiceType: {serviceType}");
                    return Content(bAcceptXML ?
                        HTTPResponse.BadRequestXML.Replace("%MESSAGE%", $"Unknown ServiceType: {serviceType}") :
                        HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", $"Unknown ServiceType: {serviceType}"),
                        bAcceptXML ? "application/xml" : "application/json");
            }
        }

        public async Task<IActionResult> GetViewers(string? sessionToken, string? fileFormat, bool bAcceptXML)
        {
            StringBuilder responseContent = new();

            if (bAcceptXML)
            {
                responseContent.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                responseContent.AppendLine("<Viewers>");
            }
            else
            {
                responseContent.AppendLine("[");
            }

            var viewerDescriptors = ViewersRegistry.GetViewerDescriptors(_logger, _configuration);
            for (int i = 0; i < viewerDescriptors.Count; i++)
            {
                var viewerDescriptor = viewerDescriptors[i];
                if (viewerDescriptor == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(viewerDescriptor.BackEnd))
                {
                    continue;
                }

                if (viewerDescriptor.BackEnd.StartsWith("{", StringComparison.OrdinalIgnoreCase) &&
                    viewerDescriptor.BackEnd.EndsWith("}", StringComparison.OrdinalIgnoreCase))
                {
                    //
                    // JSON
                    //

                    try
                    {
                        dynamic jsonDoc = JsonConvert.DeserializeObject(viewerDescriptor.BackEnd);

                        var formats = jsonDoc?.SupportedOptions?.FileFormats?.Format;
                        if (formats == null)
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(fileFormat))
                        {
                            bool bSuccess = false;
                            foreach (var format in formats)
                            {
                                string? extension = format?.extension?.ToString();
                                if (!string.IsNullOrEmpty(extension) && extension.Equals(fileFormat, StringComparison.OrdinalIgnoreCase))
                                {
                                    bSuccess = true;
                                    break;
                                }
                            }

                            if (!bSuccess)
                            {
                                continue;
                            }
                        }
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                    {
                        _logger.LogError($"Property binding error extracting file formats: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error extracting file formats: {ex.Message}");
                    }
                } // JSON
                else
                {
                    //
                    // XML
                    //

                    if (!string.IsNullOrEmpty(fileFormat))
                    {
                        XmlDocument xmlDoc = new();
                        xmlDoc.LoadXml("<BackEnd>" + viewerDescriptor.BackEnd + "</BackEnd>");

                        var formatExtension = xmlDoc.SelectSingleNode($"//Format[@extension=\"{fileFormat}\"]");
                        if (formatExtension == null)
                        {
                            continue;
                        }
                    }
                } // XML

                responseContent.AppendLine(bAcceptXML ? "\t<Viewer>" : "\t{");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<Id>{viewerDescriptor.Id}</Id>" : $"\t\t\"Id\": \"{viewerDescriptor.Id}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ProviderId>{viewerDescriptor.ProviderId}</ProviderId>" : $"\t\t\"ProviderId\": \"{viewerDescriptor.ProviderId}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceName>{viewerDescriptor.ServiceName}</ServiceName>" : $"\t\t\"ServiceName\": \"{viewerDescriptor.ServiceName}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceType>{viewerDescriptor.ServiceType}</ServiceType>" : $"\t\t\"ServiceType\": \"{viewerDescriptor.ServiceType}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<EndPoint>{viewerDescriptor.EndPoint}</EndPoint>" : $"\t\t\"EndPoint\": \"{viewerDescriptor.EndPoint}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<SessionToken>{sessionToken}</SessionToken>" : $"\t\t\"SessionToken\": \"{sessionToken}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<BackEnd>{FormatXml(viewerDescriptor.BackEnd)}</BackEnd>" : $"\t\t\"BackEnd\": \"{viewerDescriptor.BackEnd}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<FrontEnd>{FormatXml(viewerDescriptor.FrontEnd)}</FrontEnd>" : $"\t\t\"FrontEnd\": \"{viewerDescriptor.FrontEnd}\"");
                responseContent.AppendLine(bAcceptXML ? "\t</Viewer>" : (i == viewerDescriptors.Count - 1 ? "\t}" : "\t},"));
            } // for (int i = ...

            if (bAcceptXML)
            {
                responseContent.AppendLine("</Viewers>");
            }
            else
            {
                responseContent.AppendLine("]");
            }

            return Content(
                responseContent.ToString(),
                bAcceptXML ? "application/xml" : "application/json");
        }

        public async Task<IActionResult> GetThumbnailGenerators(string? sessionToken, string? fileFormat, bool bAcceptXML)
        {
            StringBuilder responseContent = new();

            if (bAcceptXML)
            {
                responseContent.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                responseContent.AppendLine("<Services>");
            }
            else
            {
                responseContent.AppendLine("[");
            }

            var serviceDescriptors = ServicesRegistry.GetServiceDescriptors(_logger, _configuration);
            for (int i = 0; i < serviceDescriptors.Count; i++)
            {
                var serviceDescriptor = serviceDescriptors[i];
                if (serviceDescriptor == null)
                {
                    continue;
                }

                if (!serviceDescriptor.ServiceType.Equals("ThumbnailGenerator", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(serviceDescriptor.BackEnd))
                {
                    continue;
                }

                List<string> destinationFormats = new();

                if (serviceDescriptor.BackEnd.StartsWith("{", StringComparison.OrdinalIgnoreCase) &&
                    serviceDescriptor.BackEnd.EndsWith("}", StringComparison.OrdinalIgnoreCase))
                {
                    //
                    // JSON
                    //

                    try
                    {
                        dynamic jsonDoc = JsonConvert.DeserializeObject(serviceDescriptor.BackEnd);

                        var formats = jsonDoc?.BackEnd?.FileFormats?.Format;
                        if (formats == null)
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(fileFormat))
                        {
                            bool bSuccess = false;
                            foreach (var format in formats)
                            {
                                string? extension = format?.extension?.ToString();
                                if (!string.IsNullOrEmpty(extension) && extension.Equals(fileFormat, StringComparison.OrdinalIgnoreCase))
                                {
                                    bSuccess = true;
                                    break;
                                }
                            }

                            if (!bSuccess)
                            {
                                continue;
                            }
                        }
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                    {
                        _logger.LogError($"Property binding error extracting file formats: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error extracting file formats: {ex.Message}");
                    }
                } // JSON
                else
                {
                    //
                    // XML
                    //

                    if (!string.IsNullOrEmpty(fileFormat))
                    {
                        XmlDocument xmlDoc = new();
                        xmlDoc.LoadXml("<BackEnd>" + serviceDescriptor.BackEnd + "</BackEnd>");

                        var formatExtension = xmlDoc.SelectSingleNode($"//Format[@extension=\"{fileFormat}\"]");
                        if (formatExtension == null)
                        {
                            continue;
                        }
                    }
                } // XML

                responseContent.AppendLine(bAcceptXML ? "\t<Service>" : "\t{");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<Id>{serviceDescriptor.Id}</Id>" : $"\t\t\"Id\": \"{serviceDescriptor.Id}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ProviderId>{serviceDescriptor.ProviderId}</ProviderId>" : $"\t\t\"ProviderId\": \"{serviceDescriptor.ProviderId}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceName>{serviceDescriptor.ServiceName}</ServiceName>" : $"\t\t\"ServiceName\": \"{serviceDescriptor.ServiceName}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceType>{serviceDescriptor.ServiceType}</ServiceType>" : $"\t\t\"ServiceType\": \"{serviceDescriptor.ServiceType}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<EndPoint>{serviceDescriptor.EndPoint}</EndPoint>" : $"\t\t\"EndPoint\": \"{serviceDescriptor.EndPoint}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<SessionToken>{sessionToken}</SessionToken>" : $"\t\t\"SessionToken\": \"{sessionToken}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<BackEnd>{FormatXml(serviceDescriptor.BackEnd)}</BackEnd>" : $"\t\t\"BackEnd\": \"{serviceDescriptor.BackEnd}\"");
                responseContent.AppendLine(bAcceptXML ? "\t</Service>" : (i == serviceDescriptors.Count - 1 ? "\t}" : "\t},"));
            } // for (int i = ...

            if (bAcceptXML)
            {
                responseContent.AppendLine("</Services>");
            }
            else
            {
                responseContent.AppendLine("]");
            }

            return Content(
                responseContent.ToString(),
                bAcceptXML ? "application/xml" : "application/json");
        }

        public async Task<IActionResult> GetConverters(string? sessionToken, string? originFormat, bool bAcceptXML)
        {
            StringBuilder responseContent = new();

            if (bAcceptXML)
            {
                responseContent.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                responseContent.AppendLine("<Services>");
            }
            else
            {
                responseContent.AppendLine("[");
            }

            var serviceDescriptors = ServicesRegistry.GetServiceDescriptors(_logger, _configuration);
            for (int i = 0; i < serviceDescriptors.Count; i++)
            {
                var serviceDescriptor = serviceDescriptors[i];
                if (serviceDescriptor == null)
                {
                    continue;
                }

                if (!serviceDescriptor.ServiceType.Equals("Converter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(serviceDescriptor.BackEnd))
                {
                    continue;
                }

                List<string> destinationFormats = new();

                if (serviceDescriptor.BackEnd.StartsWith("{", StringComparison.OrdinalIgnoreCase) &&
                    serviceDescriptor.BackEnd.EndsWith("}", StringComparison.OrdinalIgnoreCase))
                {
                    //
                    // JSON
                    //

                    try
                    {
                        
                        dynamic jsonDoc = JsonConvert.DeserializeObject(serviceDescriptor.BackEnd);

                        var conversions = jsonDoc?.BackEnd?.Conversions?.Conversion;
                        if (conversions == null)
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(originFormat))
                        {
                            foreach (var conversion in conversions)
                            {
                                var originFormatNode = conversion?.OriginFormat;
                                if (originFormatNode == null)
                                {
                                    break;
                                }

                                string? extension = originFormatNode?.extension?.ToString();
                                if (!string.IsNullOrEmpty(extension) && extension.Equals(originFormat, StringComparison.OrdinalIgnoreCase))
                                {
                                    var destinationFormatNode = conversion?.DestinationFormat;
                                    if (destinationFormatNode != null)
                                    {
                                        var destinationExtension = destinationFormatNode?.extension?.ToString();
                                        if (!string.IsNullOrEmpty(destinationExtension))
                                        {
                                            destinationFormats.Add(destinationExtension);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                    {
                        _logger.LogError($"Property binding error extracting file formats: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error extracting file formats: {ex.Message}");
                    }
                } // JSON
                else
                {
                    //
                    // XML
                    //

                    if (!string.IsNullOrEmpty(originFormat))
                    {
                        XmlDocument xmlDoc = new();
                        xmlDoc.LoadXml("<Service>" + serviceDescriptor.BackEnd + "</Service>");

                        var conversionNodes = xmlDoc.SelectNodes("//Conversions/Conversion");
                        if (conversionNodes == null)
                        {
                            continue;
                        }

                        foreach (XmlNode conversion in conversionNodes)
                        {
                            var originFormatNode = conversion.SelectSingleNode($"./OriginFormat[@extension=\"{originFormat}\"]");
                            if (originFormatNode == null)
                            {
                                continue;
                            }

                            var destinationFormatNode = conversion.SelectSingleNode("./DestinationFormat");
                            if (destinationFormatNode != null)
                            {
                                var extensionAttr = destinationFormatNode.Attributes?["extension"];
                                if (extensionAttr != null)
                                {
                                    destinationFormats.Add(extensionAttr.Value);
                                }
                            }
                        }
                    }
                } // XML

                responseContent.AppendLine(bAcceptXML ? "\t<Service>" : "{");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<Id>{serviceDescriptor.Id}</Id>" : $"\"Id\": \"{serviceDescriptor.Id}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ProviderId>{serviceDescriptor.ProviderId}</ProviderId>" : $"\"ProviderId\": \"{serviceDescriptor.ProviderId}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceName>{serviceDescriptor.ServiceName}</ServiceName>" : $"\t\t\"ServiceName\": \"{serviceDescriptor.ServiceName}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceType>{serviceDescriptor.ServiceType}</ServiceType>" : $"\"ServiceType\": \"{serviceDescriptor.ServiceType}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<EndPoint>{serviceDescriptor.EndPoint}</EndPoint>" : $"\"EndPoint\": \"{serviceDescriptor.EndPoint}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<SessionToken>{sessionToken}</SessionToken>" : $"\t\t\"SessionToken\": \"{sessionToken}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<BackEnd>{FormatXml(serviceDescriptor.BackEnd)}</BackEnd>" : $"\"BackEnd\": \"{serviceDescriptor.BackEnd}\"");
                responseContent.AppendLine(bAcceptXML ? "\t</Service>" : (i == serviceDescriptors.Count - 1 ? "}" : "},"));
            } // for (int i = ...

            if (bAcceptXML)
            {
                responseContent.AppendLine("</Services>");
            }
            else
            {
                responseContent.AppendLine("]");
            }

            return Content(
                responseContent.ToString(),
                bAcceptXML ? "application/xml" : "application/json");
        }

        public async Task<IActionResult> GetMeshFilters(string? sessionToken, bool bAcceptXML)
        {
            StringBuilder responseContent = new();

            if (bAcceptXML)
            {
                responseContent.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                responseContent.AppendLine("<Services>");
            }
            else
            {
                responseContent.AppendLine("[");
            }

            var serviceDescriptors = ServicesRegistry.GetServiceDescriptors(_logger, _configuration);
            for (int i = 0; i < serviceDescriptors.Count; i++)
            {
                var serviceDescriptor = serviceDescriptors[i];
                if (serviceDescriptor == null)
                {
                    continue;
                }

                if (!serviceDescriptor.ServiceType.Equals("MeshFilter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(serviceDescriptor.BackEnd))
                {
                    continue;
                }

                List<Tuple<string, string>> filterDescriptions = new();

                if (serviceDescriptor.BackEnd.StartsWith("{", StringComparison.OrdinalIgnoreCase) &&
                    serviceDescriptor.BackEnd.EndsWith("}", StringComparison.OrdinalIgnoreCase))
                {
                    //
                    // JSON
                    //

                    try
                    {

                        dynamic jsonDoc = JsonConvert.DeserializeObject(serviceDescriptor.BackEnd);

                        var filters = jsonDoc?.BackEnd?.Filters?.Filter;
                        if (filters == null)
                        {
                            continue;
                        }

                        foreach (var filter in filters)
                        {
                            var id = filter?.Id;
                            if (id == null)
                            {
                                continue;
                            }

                            var name = filter?.Name;
                            if (name == null)
                            {
                                continue;
                            }

                            filterDescriptions.Add(new Tuple<string, string>(id.ToString(), name.ToString()));
                        }
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                    {
                        _logger.LogError($"Property binding error extracting file formats: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error extracting file formats: {ex.Message}");
                    }
                } // JSON
                else
                {
                    //
                    // XML
                    //

                    XmlDocument xmlDoc = new();
                    xmlDoc.LoadXml("<Service>" + serviceDescriptor.BackEnd + "</Service>");

                    var filterNodes = xmlDoc.SelectNodes("//Filters/Filter");
                    if (filterNodes == null)
                    {
                        continue;
                    }

                    foreach (XmlNode filter in filterNodes)
                    {
                        var idNode = filter.SelectSingleNode("./Id");
                        if (idNode == null)
                        {
                            continue;
                        }

                        var nameNode = filter.SelectSingleNode("./Name");
                        if (nameNode == null)
                        {
                            continue;
                        }

                        filterDescriptions.Add(new Tuple<string, string>(idNode.InnerText, nameNode.InnerText));
                    }
                } // XML

                responseContent.AppendLine(bAcceptXML ? "\t<Service>" : "{");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<Id>{serviceDescriptor.Id}</Id>" : $"\"Id\": \"{serviceDescriptor.Id}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ProviderId>{serviceDescriptor.ProviderId}</ProviderId>" : $"\"ProviderId\": \"{serviceDescriptor.ProviderId}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceName>{serviceDescriptor.ServiceName}</ServiceName>" : $"\t\t\"ServiceName\": \"{serviceDescriptor.ServiceName}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceType>{serviceDescriptor.ServiceType}</ServiceType>" : $"\"ServiceType\": \"{serviceDescriptor.ServiceType}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<EndPoint>{serviceDescriptor.EndPoint}</EndPoint>" : $"\"EndPoint\": \"{serviceDescriptor.EndPoint}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<SessionToken>{sessionToken}</SessionToken>" : $"\t\t\"SessionToken\": \"{sessionToken}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<BackEnd>{FormatXml(serviceDescriptor.BackEnd)}</BackEnd>" : $"\"BackEnd\": \"{serviceDescriptor.BackEnd}\"");
                responseContent.AppendLine(bAcceptXML ? "\t</Service>" : (i == serviceDescriptors.Count - 1 ? "}" : "},"));
            } // for (int i = ...

            if (bAcceptXML)
            {
                responseContent.AppendLine("</Services>");
            }
            else
            {
                responseContent.AppendLine("]");
            }

            return Content(
                responseContent.ToString(),
                bAcceptXML ? "application/xml" : "application/json");
        }

        public async Task<IActionResult> GetPhotogrammetryServices(string? sessionToken, bool bAcceptXML)
        {
            StringBuilder responseContent = new();

            if (bAcceptXML)
            {
                responseContent.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                responseContent.AppendLine("<Services>");
            }
            else
            {
                responseContent.AppendLine("[");
            }

            var serviceDescriptors = ServicesRegistry.GetServiceDescriptors(_logger, _configuration);
            for (int i = 0; i < serviceDescriptors.Count; i++)
            {
                var serviceDescriptor = serviceDescriptors[i];
                if (serviceDescriptor == null)
                {
                    continue;
                }

                if (!serviceDescriptor.ServiceType.Equals("Photogrammetry", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(serviceDescriptor.BackEnd))
                {
                    continue;
                }

                List<Tuple<string, string>> workflowDescriptions = new();

                if (serviceDescriptor.BackEnd.StartsWith("{", StringComparison.OrdinalIgnoreCase) &&
                    serviceDescriptor.BackEnd.EndsWith("}", StringComparison.OrdinalIgnoreCase))
                {
                    //
                    // JSON
                    //

                    try
                    {

                        dynamic jsonDoc = JsonConvert.DeserializeObject(serviceDescriptor.BackEnd);

                        var workflows = jsonDoc?.BackEnd?.Workflows?.Workflow;
                        if (workflows == null)
                        {
                            continue;
                        }

                        foreach (var workflow in workflows)
                        {
                            var id = workflow?.Id;
                            if (id == null)
                            {
                                continue;
                            }

                            var name = workflow?.Name;
                            if (name == null)
                            {
                                continue;
                            }

                            workflowDescriptions.Add(new Tuple<string, string>(id.ToString(), name.ToString()));
                        }
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                    {
                        _logger.LogError($"Property binding error extracting file formats: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error extracting file formats: {ex.Message}");
                    }
                } // JSON
                else
                {
                    //
                    // XML
                    //

                    XmlDocument xmlDoc = new();
                    xmlDoc.LoadXml("<Service>" + serviceDescriptor.BackEnd + "</Service>");

                    var workflowNodes = xmlDoc.SelectNodes("//Workflows/Workflow");
                    if (workflowNodes == null)
                    {
                        continue;
                    }

                    foreach (XmlNode workflow in workflowNodes)
                    {
                        var idNode = workflow.SelectSingleNode("./Id");
                        if (idNode == null)
                        {
                            continue;
                        }

                        var nameNode = workflow.SelectSingleNode("./Name");
                        if (nameNode == null)
                        {
                            continue;
                        }

                        workflowDescriptions.Add(new Tuple<string, string>(idNode.InnerText, nameNode.InnerText));
                    }
                } // XML

                responseContent.AppendLine(bAcceptXML ? "\t<Service>" : "{");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<Id>{serviceDescriptor.Id}</Id>" : $"\"Id\": \"{serviceDescriptor.Id}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ProviderId>{serviceDescriptor.ProviderId}</ProviderId>" : $"\"ProviderId\": \"{serviceDescriptor.ProviderId}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceName>{serviceDescriptor.ServiceName}</ServiceName>" : $"\t\t\"ServiceName\": \"{serviceDescriptor.ServiceName}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceType>{serviceDescriptor.ServiceType}</ServiceType>" : $"\"ServiceType\": \"{serviceDescriptor.ServiceType}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<EndPoint>{serviceDescriptor.EndPoint}</EndPoint>" : $"\"EndPoint\": \"{serviceDescriptor.EndPoint}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<SessionToken>{sessionToken}</SessionToken>" : $"\t\t\"SessionToken\": \"{sessionToken}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<BackEnd>{FormatXml(serviceDescriptor.BackEnd)}</BackEnd>" : $"\"BackEnd\": \"{serviceDescriptor.BackEnd}\"");
                responseContent.AppendLine(bAcceptXML ? "\t</Service>" : (i == serviceDescriptors.Count - 1 ? "}" : "},"));
            } // for (int i = ...

            if (bAcceptXML)
            {
                responseContent.AppendLine("</Services>");
            }
            else
            {
                responseContent.AppendLine("]");
            }

            return Content(
                responseContent.ToString(),
                bAcceptXML ? "application/xml" : "application/json");
        }

        public async Task<IActionResult> GetRepositoryServices(string? sessionToken, bool bAcceptXML)
        {
            StringBuilder responseContent = new();

            if (bAcceptXML)
            {
                responseContent.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                responseContent.AppendLine("<Services>");
            }
            else
            {
                responseContent.AppendLine("[");
            }

            var serviceDescriptors = ServicesRegistry.GetServiceDescriptors(_logger, _configuration);
            for (int i = 0; i < serviceDescriptors.Count; i++)
            {
                var serviceDescriptor = serviceDescriptors[i];
                if (serviceDescriptor == null)
                {
                    continue;
                }

                if (!serviceDescriptor.ServiceType.Equals("Repository", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(serviceDescriptor.BackEnd))
                {
                    continue;
                }

                if (serviceDescriptor.BackEnd.StartsWith("{", StringComparison.OrdinalIgnoreCase) &&
                    serviceDescriptor.BackEnd.EndsWith("}", StringComparison.OrdinalIgnoreCase))
                {
                    //
                    // JSON
                    //

                    try
                    {

                        dynamic jsonDoc = JsonConvert.DeserializeObject(serviceDescriptor.BackEnd);

                        var workflows = jsonDoc?.BackEnd?.Workflows?.Workflow;
                        if (workflows == null)
                        {
                            continue;
                        }

                        foreach (var workflow in workflows)
                        {
                            var id = workflow?.Id;
                            if (id == null)
                            {
                                continue;
                            }

                            var name = workflow?.Name;
                            if (name == null)
                            {
                                continue;
                            }
                        }
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                    {
                        _logger.LogError($"Property binding error extracting file formats: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error extracting file formats: {ex.Message}");
                    }
                } // JSON
                else
                {
                    //
                    // XML
                    //

                    XmlDocument xmlDoc = new();
                    xmlDoc.LoadXml("<Service>" + serviceDescriptor.BackEnd + "</Service>");

                    var workflowNodes = xmlDoc.SelectNodes("//Workflows/Workflow");
                    if (workflowNodes == null)
                    {
                        continue;
                    }

                    foreach (XmlNode workflow in workflowNodes)
                    {
                        var idNode = workflow.SelectSingleNode("./Id");
                        if (idNode == null)
                        {
                            continue;
                        }

                        var nameNode = workflow.SelectSingleNode("./Name");
                        if (nameNode == null)
                        {
                            continue;
                        }
                    }
                } // XML

                responseContent.AppendLine(bAcceptXML ? "\t<Service>" : "{");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<Id>{serviceDescriptor.Id}</Id>" : $"\"Id\": \"{serviceDescriptor.Id}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ProviderId>{serviceDescriptor.ProviderId}</ProviderId>" : $"\"ProviderId\": \"{serviceDescriptor.ProviderId}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceName>{serviceDescriptor.ServiceName}</ServiceName>" : $"\"ServiceName\": \"{serviceDescriptor.ServiceName}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<ServiceType>{serviceDescriptor.ServiceType}</ServiceType>" : $"\"ServiceType\": \"{serviceDescriptor.ServiceType}\",");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<EndPoint>{serviceDescriptor.EndPoint}</EndPoint>" : $"\"EndPoint\": \"{serviceDescriptor.EndPoint}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<UploadEndPoint>{serviceDescriptor.UploadEndPoint}</UploadEndPoint>" : $"\"UploadEndPoint\": \"{serviceDescriptor.UploadEndPoint}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<SessionToken>{sessionToken}</SessionToken>" : $"\t\t\"SessionToken\": \"{sessionToken}\"");
                responseContent.AppendLine(bAcceptXML ? $"\t\t<BackEnd>{FormatXml(serviceDescriptor.BackEnd)}</BackEnd>" : $"\"BackEnd\": \"{serviceDescriptor.BackEnd}\"");
                responseContent.AppendLine(bAcceptXML ? "\t</Service>" : (i == serviceDescriptors.Count - 1 ? "}" : "},"));
            } // for (int i = ...

            if (bAcceptXML)
            {
                responseContent.AppendLine("</Services>");
            }
            else
            {
                responseContent.AppendLine("]");
            }

            return Content(
                responseContent.ToString(),
                bAcceptXML ? "application/xml" : "application/json");
        }

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

            dynamic jsonRequest = JsonConvert.DeserializeObject(body);
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
                if (jsonRequest.Protocol?.AuthorizationRequest != null)
                {
                    return AuthorizeJSON(jsonRequest);
                }

                var serviceType = jsonRequest.Protocol?.RegistrationRequest?.ServiceType?.ToString();
                if (string.IsNullOrEmpty(serviceType))
                {
                    _logger.LogError("Missing 'ServiceType' in registration request.");
                    return Content(HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Missing 'ServiceType' in registration request."), "application/json");
                }

                switch (serviceType)
                {
                    case "Viewer":
                        return RegisterViewerJSON(jsonRequest);
                    case "ThumbnailGenerator":
                    case "Converter":
                    case "MeshFilter":
                    case "Photogrammetry":
                    case "Repository":
                        return RegisterServiceJSON(serviceType, jsonRequest);
                    default:
                        _logger.LogError($"Unknown ServiceType: {serviceType}");
                        return Content(HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", $"Unknown ServiceType: {serviceType}"), "application/json");
                }
            }
            else
            {
                XmlDocument xmlRequest = new XmlDocument();
                try
                {
                    var xmlBody = JsonConvert.DeserializeObject<string>(body);
                    xmlRequest.LoadXml(xmlBody);
                }
                catch (XmlException ex)
                {
                    _logger.LogError($"XML parsing error: {ex.Message}");
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "XML parsing error."));
                }

                var authorizationRequest = xmlRequest.SelectSingleNode("/Protocol/AuthorizationRequest");
                if (authorizationRequest != null)
                {
                    return AuthorizeXML(xmlRequest);
                }

                var serviceType = xmlRequest.SelectSingleNode("/Protocol/RegistrationRequest/ServiceType")?.InnerText;
                if (string.IsNullOrEmpty(serviceType))
                {
                    _logger.LogError("Missing 'ServiceType' in registration request.");
                    return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", "Missing 'ServiceType' in registration request."), "application/xml");
                }

                switch (serviceType)
                {
                    case "Viewer":
                        return RegisterViewerXML(xmlRequest);
                    case "ThumbnailGenerator":
                    case "Converter":
                    case "MeshFilter":
                    case "Photogrammetry":
                    case "Repository":
                        return RegisterServiceXML(serviceType, xmlRequest);
                    default:
                        _logger.LogError($"Unknown ServiceType: {serviceType}");
                        return Content(HTTPResponse.BadRequestXML.Replace("%MESSAGE%", $"Unknown ServiceType: {serviceType}"), "application/xml");
                }
            }
        }

        public async Task<IActionResult> OnPostAuthorizeAsync()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrEmpty(body))
            {
                _logger.LogInformation("Received empty request.");
                return Content(authorizationResponseErrorXML.Replace("%MESSAGE%", "Received empty request."));
            }

            var xmlRequest = JsonConvert.DeserializeObject<string>(body);
            if (string.IsNullOrEmpty(xmlRequest))
            {
                _logger.LogError("Received empty request.");
                return Content(authorizationResponseErrorXML.Replace("%MESSAGE%", "Received empty request."));
            }

            _logger.LogInformation($"****** AuthorizationRequest ******\n{xmlRequest}");

            XmlDocument xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.LoadXml(xmlRequest);
            }
            catch (XmlException ex)
            {
                _logger.LogError($"XML parsing error: {ex.Message}");
                return Content(authorizationResponseErrorXML.Replace("%MESSAGE%", "XML parsing error."));
            }

            return AuthorizeXML(xmlDoc);
        }

        private IActionResult AuthorizeJSON(dynamic jsonDoc)
        {
            if (jsonDoc == null)
            {
                _logger.LogError("Received null JSON document.");
                return Content(HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Received null JSON document."), "application/json");
            }

            string? providerId = jsonDoc?.Protocol?.AuthorizationRequest?.ProviderID?.ToString();
            if (string.IsNullOrEmpty(providerId))
            {
                _logger.LogError("Bad request: 'ProviderID'.");
                return Content(authorizationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'ProviderID'."));
            }

            //#todo: validate providerId or move it to the OnPost method

            if (!AuthorizationRequests.TryGetValue(providerId, out var authorizationRequest))
            {
                authorizationRequest = new AuthorizationRequest
                {
                    ProviderId = providerId,
                    SessionToken = Guid.NewGuid().ToString()//#todo: JWT? expiration time?
                };
            }

            _logger.LogInformation($"Session token: {authorizationRequest.SessionToken} for provider ID: {providerId}");

            AuthorizationRequests.AddOrUpdate(providerId, authorizationRequest, (_, oldValue) =>
            {
                oldValue.TimeStamp = DateTime.Now;
                return oldValue;
            });

            return Content(
                authorizationResponseJSON.Replace("%SESSION_TOKEN%", authorizationRequest.SessionToken),
                "application/json");
        }

        private IActionResult AuthorizeXML(XmlDocument xmlDoc)
        {
            if (xmlDoc == null)
            {
                _logger.LogError("Received null XML document.");
                return Content(authorizationResponseErrorXML.Replace("%MESSAGE%", "Received null XML document."));
            }

            var providerId = xmlDoc.SelectSingleNode("/Protocol/AuthorizationRequest/ProviderID")?.InnerText;
            if (string.IsNullOrEmpty(providerId))
            {
                _logger.LogError("Bad request: 'ProviderID'.");
                return Content(authorizationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'ProviderID'."));
            }

            //#todo: validate providerId or move it to the OnPost method

            if (!AuthorizationRequests.TryGetValue(providerId, out var authorizationRequest))
            {
                authorizationRequest = new AuthorizationRequest
                {
                    ProviderId = providerId,
                    SessionToken = Guid.NewGuid().ToString()//#todo: JWT? expiration time?
                };
            }

            _logger.LogInformation($"Session token: {authorizationRequest.SessionToken} for provider ID: {providerId}");

            AuthorizationRequests.AddOrUpdate(providerId, authorizationRequest, (_, oldValue) =>
            {
                oldValue.TimeStamp = DateTime.Now;
                return oldValue;
            });

            return Content(authorizationResponseXML.Replace("%SESSION_TOKEN%", authorizationRequest.SessionToken));
        }

        private IActionResult RegisterViewerXML(XmlDocument xmlDoc)
        {
            if (xmlDoc == null)
            {
                _logger.LogError("Received null XML document.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Received null XML document."));
            }

            var providerId = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/ProviderID")?.InnerText;
            if (string.IsNullOrEmpty(providerId))
            {
                _logger.LogError("Bad request: 'ProviderID'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'ProviderID'."));
            }

            var serviceName = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/ServiceName")?.InnerText;

            //#todo: validate providerId or move it to the OnPost method

            if (!AuthorizationRequests.TryGetValue(providerId, out var authorizationRequest))
            {
                _logger.LogError($"Provider '{providerId}' is not authorized.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Provider is not authorized."));
            }

            var sessionToken = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/SessionToken")?.InnerText;
            if (string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogError("Bad request: 'SessionToken'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'SessionToken'."));
            }

            if (sessionToken != authorizationRequest.SessionToken)
            {
                _logger.LogError("Bad request: invalid 'SessionToken'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: invalid 'SessionToken'."));
            }

            var endPoint = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/EndPoint")?.InnerText;
            if (string.IsNullOrEmpty(endPoint))
            {
                _logger.LogError("Bad request: 'EndPoint'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'EndPoint'."));
            }

            if (ViewersRegistry.IsViewerRegistered(_logger, _configuration, endPoint))
            {
                _logger.LogError($"Viewer is already registered 'Endpoint': {endPoint}");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Viewer is already registered."));
            }

            var backEnd = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/BackEnd")?.InnerXml;
            if (string.IsNullOrEmpty(backEnd))
            {
                _logger.LogError("Bad request: 'BackEnd'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'BackEnd'."));
            }

            var frontEnd = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/FrontEnd")?.InnerXml;
            if (string.IsNullOrEmpty(frontEnd))
            {
                _logger.LogError("Bad request: 'FrontEnd'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'FrontEnd'."));
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var viewersDir = _configuration[$"{FileStorage}:ViewersDir"];
            if (string.IsNullOrEmpty(viewersDir))
            {
                _logger.LogError("Viewers path is not configured.");
                return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Viewers path is not configured."), "application/xml");
            }

            if (!Directory.Exists(viewersDir))
            {
                _logger.LogError($"Viewers directory does not exist: {viewersDir}");
                return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Viewers directory does not exist."), "application/xml");
            }

            var viewerId = Guid.NewGuid().ToString();
            _logger.LogInformation($"Viewer registered with ID: {viewerId}");

            StringBuilder xml = new();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<Viewer>");
            xml.AppendLine($"\t<Id>{viewerId}</Id>");
            xml.AppendLine($"\t<ProviderId>{providerId}</ProviderId>");
            xml.AppendLine($"\t<ServiceName>{serviceName}</ServiceName>");
            xml.AppendLine($"\t<EndPoint>{endPoint}</EndPoint>");
            xml.AppendLine($"\t<BackEnd>{backEnd}</BackEnd>");
            xml.AppendLine($"\t<FrontEnd>{frontEnd}</FrontEnd>");
            xml.AppendLine($"\t<TimeStamp>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</TimeStamp>");
            xml.AppendLine("</Viewer>");
            System.IO.File.WriteAllText(Path.Combine(viewersDir, $"{viewerId}.xml"), xml.ToString());

            return Content(registrationResponseXML.Replace("%SESSION_TOKEN%", sessionToken));
        }

        private IActionResult RegisterViewerJSON(dynamic jsonDoc)
        {
            if (jsonDoc == null)
            {
                _logger.LogError("Received null JSON document.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Received null JSON document."), "application/json");
            }

            // Extract ProviderID from JSON structure: Protocol.RegistrationRequest.ProviderID
            string? providerId = null;
            try
            {
                providerId = jsonDoc?.Protocol?.RegistrationRequest?.ProviderID?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting ProviderID from JSON: {ex.Message}");
            }

            if (string.IsNullOrEmpty(providerId))
            {
                _logger.LogError("Bad request: 'ProviderID'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'ProviderID'."), "application/json");
            }

            //#todo: validate providerId or move it to the OnPost method

            if (!AuthorizationRequests.TryGetValue(providerId, out var authorizationRequest))
            {
                _logger.LogError($"Provider '{providerId}' is not authorized.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Provider is not authorized."), "application/json");
            }

            // Extract SessionToken from JSON
            string? sessionToken = null;
            try
            {
                sessionToken = jsonDoc?.Protocol?.RegistrationRequest?.SessionToken?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting SessionToken from JSON: {ex.Message}");
            }

            if (string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogError("Bad request: 'SessionToken'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'SessionToken'."), "application/json");
            }

            if (sessionToken != authorizationRequest.SessionToken)
            {
                _logger.LogError("Bad request: invalid 'SessionToken'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: invalid 'SessionToken'."), "application/json");
            }

            // Extract EndPoint from JSON
            string? endPoint = null;
            try
            {
                endPoint = jsonDoc?.Protocol?.RegistrationRequest?.EndPoint?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting EndPoint from JSON: {ex.Message}");
            }

            if (string.IsNullOrEmpty(endPoint))
            {
                _logger.LogError("Bad request: 'EndPoint'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'EndPoint'."), "application/json");
            }

            if (ViewersRegistry.IsViewerRegistered(_logger, _configuration, endPoint))
            {
                _logger.LogError($"Viewer is already registered 'Endpoint': {endPoint}");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Viewer is already registered."), "application/json");
            }

            // Extract BackEnd from JSON - convert to XML string for storage
            string? backEnd = null;
            try
            {
                var backEndObj = jsonDoc?.Protocol?.RegistrationRequest?.BackEnd;
                if (backEndObj != null)
                {
                    // Convert JSON BackEnd object back to XML format for compatibility with existing storage
                    backEnd = JsonConvert.SerializeObject(backEndObj, Newtonsoft.Json.Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting BackEnd from JSON: {ex.Message}");
            }

            if (string.IsNullOrEmpty(backEnd))
            {
                _logger.LogError("Bad request: 'BackEnd'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'BackEnd'."), "application/json");
            }

            // Extract FrontEnd from JSON - convert to XML string for storage
            string? frontEnd = null;
            try
            {
                var frontEndObj = jsonDoc?.Protocol?.RegistrationRequest?.FrontEnd;
                if (frontEndObj != null)
                {
                    // Convert JSON FrontEnd object back to XML format for compatibility with existing storage
                    frontEnd = JsonConvert.SerializeObject(frontEndObj, Newtonsoft.Json.Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting FrontEnd from JSON: {ex.Message}");
            }

            if (string.IsNullOrEmpty(frontEnd))
            {
                _logger.LogError("Bad request: 'FrontEnd'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'FrontEnd'."), "application/json");
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var viewersDir = _configuration[$"{FileStorage}:ViewersDir"];
            if (string.IsNullOrEmpty(viewersDir))
            {
                _logger.LogError("Viewers path is not configured.");
                return Content(HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Viewers path is not configured."), "application/json");
            }

            if (!Directory.Exists(viewersDir))
            {
                _logger.LogError($"Viewers directory does not exist: {viewersDir}");
                return Content(HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Viewers directory does not exist."), "application/json");
            }

            var viewerId = Guid.NewGuid().ToString();
            _logger.LogInformation($"Viewer registered with ID: {viewerId}");

            // Store as XML for compatibility with existing system
            StringBuilder xml = new();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<Viewer>");
            xml.AppendLine($"\t<ProviderId>{providerId}</ProviderId>");
            xml.AppendLine($"\t<Id>{viewerId}</Id>");
            xml.AppendLine($"\t<EndPoint>{endPoint}</EndPoint>");
            xml.AppendLine($"\t<BackEnd>{System.Security.SecurityElement.Escape(backEnd)}</BackEnd>");
            xml.AppendLine($"\t<FrontEnd>{System.Security.SecurityElement.Escape(frontEnd)}</FrontEnd>");
            xml.AppendLine($"\t<TimeStamp>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</TimeStamp>");
            xml.AppendLine("</Viewer>");
            System.IO.File.WriteAllText(Path.Combine(viewersDir, $"{viewerId}.xml"), xml.ToString());

            return Content(registrationResponseJSON, "application/json");
        }

        private IActionResult RegisterServiceXML(string serviceType, XmlDocument xmlDoc)
        {
            if (xmlDoc == null)
            {
                _logger.LogError("Received null XML document.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Received null XML document."));
            }

            var providerId = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/ProviderID")?.InnerText;
            if (string.IsNullOrEmpty(providerId))
            {
                _logger.LogError("Bad request: 'ProviderID'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'ProviderID'."));
            }

            //#todo: validate providerId or move it to the OnPost method

            if (!AuthorizationRequests.TryGetValue(providerId, out var authorizationRequest))
            {
                _logger.LogError($"Provider '{providerId}' is not authorized.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Provider is not authorized."));
            }

            var sessionToken = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/SessionToken")?.InnerText;
            if (string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogError("Bad request: 'SessionToken'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'SessionToken'."));
            }

            if (sessionToken != authorizationRequest.SessionToken)
            {
                _logger.LogError("Bad request: invalid 'SessionToken'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: invalid 'SessionToken'."));
            }

            var serviceName = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/ServiceName")?.InnerText;
            var uploadEndpoint = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/UploadEndPoint")?.InnerText;

            var endPoint = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/EndPoint")?.InnerText;
            if (string.IsNullOrEmpty(endPoint))
            {
                _logger.LogError("Bad request: 'EndPoint'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'EndPoint'."));
            }

            if (ServicesRegistry.IsServiceRegistered(_logger, _configuration, endPoint))
            {
                _logger.LogError($"Service is already registered 'Endpoint': {endPoint}");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Service is already registered."));
            }

            var backEnd = xmlDoc.SelectSingleNode("/Protocol/RegistrationRequest/BackEnd")?.InnerXml;
            if (string.IsNullOrEmpty(backEnd))
            {
                _logger.LogError("Bad request: 'BackEnd'.");
                return Content(registrationResponseErrorXML.Replace("%MESSAGE%", "Bad request: 'BackEnd'."));
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var servicesDir = _configuration[$"{FileStorage}:ServicesDir"];
            if (string.IsNullOrEmpty(servicesDir))
            {
                _logger.LogError("Services path is not configured.");
                return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Services path is not configured."), "application/xml");
            }

            if (!Directory.Exists(servicesDir))
            {
                _logger.LogError($"Services directory does not exist: {servicesDir}");
                return Content(HTTPResponse.ServerErrorXML.Replace("%MESSAGE%", "Services directory does not exist."), "application/xml");
            }

            var serviceId = Guid.NewGuid().ToString();
            _logger.LogInformation($"Service registered with ID: {serviceId}");

            StringBuilder xml = new();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<Service>");
            xml.AppendLine($"\t<Id>{serviceId}</Id>");
            xml.AppendLine($"\t<ProviderId>{providerId}</ProviderId>");
            xml.AppendLine($"\t<ServiceName>{serviceName}</ServiceName>");
            xml.AppendLine($"\t<ServiceType>{serviceType}</ServiceType>");            
            xml.AppendLine($"\t<EndPoint>{endPoint}</EndPoint>");
            xml.AppendLine($"\t<UploadEndPoint>{uploadEndpoint}</UploadEndPoint>");
            xml.AppendLine($"\t<BackEnd>{backEnd}</BackEnd>");
            xml.AppendLine($"\t<TimeStamp>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</TimeStamp>");
            xml.AppendLine("</Service>");
            System.IO.File.WriteAllText(Path.Combine(servicesDir, $"{serviceId}.xml"), xml.ToString());

            return Content(registrationResponseXML.Replace("%SESSION_TOKEN%", sessionToken));
        }

        private IActionResult RegisterServiceJSON(string serviceType, dynamic jsonDoc)
        {
            if (jsonDoc == null)
            {
                _logger.LogError("Received null JSON document.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Received null JSON document."), "application/json");
            }

            // Extract ProviderID from JSON structure: Protocol.RegistrationRequest.ProviderID
            string? providerId = null;
            try
            {
                providerId = jsonDoc?.Protocol?.RegistrationRequest?.ProviderID?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting ProviderID from JSON: {ex.Message}");
            }

            if (string.IsNullOrEmpty(providerId))
            {
                _logger.LogError("Bad request: 'ProviderID'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'ProviderID'."), "application/json");
            }

            //#todo: validate providerId or move it to the OnPost method

            if (!AuthorizationRequests.TryGetValue(providerId, out var authorizationRequest))
            {
                _logger.LogError($"Provider '{providerId}' is not authorized.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Provider is not authorized."), "application/json");
            }

            // Extract SessionToken from JSON
            string? sessionToken = null;
            try
            {
                sessionToken = jsonDoc?.Protocol?.RegistrationRequest?.SessionToken?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting SessionToken from JSON: {ex.Message}");
            }

            if (string.IsNullOrEmpty(sessionToken))
            {
                _logger.LogError("Bad request: 'SessionToken'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'SessionToken'."), "application/json");
            }

            if (sessionToken != authorizationRequest.SessionToken)
            {
                _logger.LogError("Bad request: invalid 'SessionToken'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: invalid 'SessionToken'."), "application/json");
            }

            // Extract EndPoint from JSON
            string? endPoint = null;
            try
            {
                endPoint = jsonDoc?.Protocol?.RegistrationRequest?.EndPoint?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting EndPoint from JSON: {ex.Message}");
            }

            if (string.IsNullOrEmpty(endPoint))
            {
                _logger.LogError("Bad request: 'EndPoint'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'EndPoint'."), "application/json");
            }

            if (ServicesRegistry.IsServiceRegistered(_logger, _configuration, endPoint))
            {
                _logger.LogError($"Service is already registered 'Endpoint': {endPoint}");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Service is already registered."), "application/json");
            }

            // Extract SupportedOptions from JSON - convert to XML string for storage
            string? supportedOptions = null;
            try
            {
                var supportedOptionsObj = jsonDoc?.Protocol?.RegistrationRequest?.SupportedOptions;
                if (supportedOptionsObj != null)
                {
                    // Convert JSON SupportedOptions object back to XML format for compatibility with existing storage
                    supportedOptions = JsonConvert.SerializeObject(supportedOptionsObj, Newtonsoft.Json.Formatting.Indented);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting SupportedOptions from JSON: {ex.Message}");
            }

            if (string.IsNullOrEmpty(supportedOptions))
            {
                _logger.LogError("Bad request: 'BackEnd'.");
                return Content(registrationResponseErrorJSON.Replace("%MESSAGE%", "Bad request: 'BackEnd'."), "application/json");
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var FileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var servicesDir = _configuration[$"{FileStorage}:ServicesDir"];
            if (string.IsNullOrEmpty(servicesDir))
            {
                _logger.LogError("Services path is not configured.");
                return Content(HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Services path is not configured."), "application/json");
            }

            if (!Directory.Exists(servicesDir))
            {
                _logger.LogError($"Services directory does not exist: {servicesDir}");
                return Content(HTTPResponse.BadRequestJSON.Replace("%MESSAGE%", "Services directory does not exist."), "application/json");
            }

            var serviceId = Guid.NewGuid().ToString();
            _logger.LogInformation($"Service registered with ID: {serviceId}");

            // Store as XML for compatibility with existing system
            StringBuilder xml = new();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<Service>");
            xml.AppendLine($"\t<ProviderId>{providerId}</ProviderId>");
            xml.AppendLine($"\t<ServiceType>{serviceType}</ServiceType>");
            xml.AppendLine($"\t<Id>{serviceId}</Id>");
            xml.AppendLine($"\t<EndPoint>{endPoint}</EndPoint>");
            xml.AppendLine($"\t<SupportedOptions>{System.Security.SecurityElement.Escape(supportedOptions)}</SupportedOptions>");
            xml.AppendLine($"\t<TimeStamp>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</TimeStamp>");
            xml.AppendLine("</Service>");
            System.IO.File.WriteAllText(Path.Combine(servicesDir, $"{serviceId}.xml"), xml.ToString());

            return Content(registrationResponseJSON, "application/json");
        }

        private static string FormatXml(string xml, string indentChars = "\t")
        {
            try
            {
                XmlDocument doc = new();
                doc.LoadXml(xml);

                using var stringWriter = new StringWriter();
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = indentChars,
                    NewLineChars = Environment.NewLine,
                    NewLineHandling = NewLineHandling.Replace
                };
                using var writer = XmlWriter.Create(stringWriter, settings);
                doc.WriteTo(writer);
                writer.Flush();
                return stringWriter.ToString();
            }
            catch (Exception)
            {
                // Return original string if parsing fails
                return xml;
            }
        }
    }
}
