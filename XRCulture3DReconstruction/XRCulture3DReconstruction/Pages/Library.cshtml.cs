using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XRCulture3DReconstruction.Models;
using XRCulture3DReconstruction.Services;

namespace XRCulture3DReconstruction.Pages
{
    [Authorize]
    public class LibraryModel : PageModel
    {
        private readonly ILogger<LibraryModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IOperationSingleton _singletonOperation;

        public LibraryModel(ILogger<LibraryModel> logger, IConfiguration configuration, IOperationSingleton singletonOperation)
        {
            _logger = logger;
            _configuration = configuration;
            _singletonOperation = singletonOperation;
        }

        public void OnGet()
        {
        }

        public IActionResult OnPostSaveModelView([FromBody] ModelViewRequest request)
        {
            if (request?.Model == null)
            {
                return BadRequest(new { success = false, message = "Model is required." });
            }

            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var modelsDir = _configuration[$"{fileStorage}:ModelsDir"]!;
            var id = Path.GetFileNameWithoutExtension(request.Model);
            var xmlPath = Path.Combine(modelsDir, id + ".xml");

            if (!System.IO.File.Exists(xmlPath))
            {
                return NotFound(new { success = false, message = $"Model XML not found: {id}" });
            }

            var xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlPath);

            var viewNode = xmlDoc.SelectSingleNode("//view");
            if (viewNode == null)
            {
                var modelNode = xmlDoc.SelectSingleNode("//model") ?? (XmlNode)xmlDoc.DocumentElement!;
                viewNode = xmlDoc.CreateElement("view");
                modelNode.AppendChild(viewNode);
            }

            UpdateViewAttribute(xmlDoc, viewNode, "showSettingsPanel", request.ShowSettingsPanel?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "showFaces", request.ShowFaces?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "showWireframes", request.ShowWireframes?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "showLines", request.ShowLines?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "showPoints", request.ShowPoints?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "showModelCS", request.ShowModelCS?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "showWorldCS", request.ShowWorldCS?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "showNavigator", request.ShowNavigator?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "backgroundColor", request.BackgroundColor?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "selectionColor", request.SelectionColor?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "rotation", request.Rotation?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "eyeVector", request.EyeVector?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "targetVector", request.TargetVector?.ToString());
            UpdateViewAttribute(xmlDoc, viewNode, "upVector", request.UpVector?.ToString());

            xmlDoc.Save(xmlPath);
            _logger.LogInformation($"Model view saved for {id}");

            return new JsonResult(new
            {
                success = true,
                message = "Model view saved successfully.",
                model = id
            });
        }

        private static void UpdateViewAttribute(XmlDocument xmlDoc, XmlNode viewNode, string elementName, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var node = viewNode.SelectSingleNode(elementName);
            if (node == null)
            {
                node = xmlDoc.CreateElement(elementName);
                viewNode.AppendChild(node);
            }

            var attr = node.Attributes!["value"];
            if (attr == null)
            {
                attr = xmlDoc.CreateAttribute("value");
                node.Attributes.Append(attr);
            }

            attr.Value = value;
        }

        public List<ModelInformation> GetModelInfos()
        {
            var isLinuxPlatform = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var fileStorage = isLinuxPlatform ? "FileStorageLinux" : "FileStorage";

            var provider = new PhysicalFileProvider(_configuration[$"{fileStorage}:ModelsDir"]!);

            var xmls = provider.GetDirectoryContents("/").Where((fileInfo) =>
            {
                if (fileInfo.IsDirectory)
                {
                    return false;
                }

                if (!fileInfo.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            });

            var binzs = provider.GetDirectoryContents("/").Where((fileInfo) =>
            {
                if (fileInfo.IsDirectory)
                {
                    return false;
                }

                if (!fileInfo.Name.EndsWith(".binz", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }).Select((f) => Path.GetFileNameWithoutExtension(f.Name)).ToList();

            var logs = provider.GetDirectoryContents("/").Where((fileInfo) =>
            {
                if (fileInfo.IsDirectory)
                {
                    return false;
                }

                if (!fileInfo.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }).Select((f) => Path.GetFileNameWithoutExtension(f.Name)).ToList();

            List<ModelInformation> xmlModelInfos = new();
            foreach (var fileInfo in xmls)
            {
                if (fileInfo?.PhysicalPath != null)
                {
                    _logger.LogInformation($"Found model: {fileInfo.Name} at {fileInfo.PhysicalPath}");

                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(fileInfo.PhysicalPath);

                    var id = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    xmlModelInfos.Add(new ModelInformation
                    {
                        Id = id,
                        Input = xmlDoc.SelectSingleNode("//model/input")?.InnerText,
                        WorkflowName = xmlDoc.SelectSingleNode("//workflow/name")?.InnerText ?? "Unknown",
                        TimeStamp = xmlDoc.SelectSingleNode("//model/timeStamp")?.InnerText ?? "Unknown",
                        ViewUrl = binzs.IndexOf(id) != -1 ? $"/Viewer?model={id}.binz" : null,
                        DownloadUrl = binzs.IndexOf(id) != -1 ? $"/Storage?handler=Model&id={id}.binz" : null,
                        LogUrl = logs.IndexOf(id) != -1 ? $"/Storage?handler=Log&id={id}.txt" : null,
                        Parameters = ParseParameters(xmlDoc),
                        View = ParseView(xmlDoc)
                    });
                }
            }

            return xmlModelInfos = xmlModelInfos
                .OrderByDescending(m =>
                {
                    if (string.IsNullOrEmpty(m.TimeStamp) || m.TimeStamp == "Unknown")
                        return DateTime.MinValue;
                    return DateTime.TryParse(m.TimeStamp, out var date) ? date : DateTime.MinValue;
                })
                .ToList();
        }

        private Dictionary<string, string> ParseParameters(XmlDocument xmlDoc)
        {
            var parameters = new Dictionary<string, string>();
            var parametersNode = xmlDoc.SelectSingleNode("//workflow/parameters");
            
            if ((parametersNode != null) && parametersNode.HasChildNodes)
            {
                foreach (XmlNode childNode in parametersNode.ChildNodes)
                {
                    if (childNode.NodeType == XmlNodeType.Element)
                    {
                        parameters[childNode.Name] = childNode.InnerText;
                    }
                }
            }
            
            return parameters;
        }

        /*
        <?xml version="1.0" encoding="UTF-8"?>
        <model>
	        <input>https://github.com/svilenvarbanov2019/xrculture_testdata/tree/main/rabbit</input>
	        <workflow>
		        <name><![CDATA[openMVG-openMVS]]></name>
		        <parameters>
			        <quality>High</quality>
		        </parameters>
	        </workflow>
	        <timeStamp>2026-05-19 09:11:00</timeStamp>
	
	        <view>
		        <showSettingsPanel value="true" />
		        <showFaces value="true" />
		        <showWireframes value="true" />
		        <showLines value="true" />
		        <showPoints value="true" />
		        <showModelCS value="true" />
		        <showWorldCS value="true" />
		        <showNavigator value="true" />
                <backgroundColor value="E6E6E6" />
	            <selectionColor value="FF0000FF" />
                <rotation value="270.00,45.00,30.00" />
                <eyeVector value="0.9900000095367432,1.4199999570846558,-4" />
                <targetVector value="0.9900000095367432,1.4199999570846558,0" />
                <upVector value="0,1,0" />
	        </view>
        </model>
        */
        private ModelView ParseView(XmlDocument xmlDoc)
        {
            var view = new ModelView();

            var viewNode = xmlDoc.SelectSingleNode("//view");
            if (viewNode != null)
            {
                var showSettingsPanel = viewNode.SelectSingleNode("showSettingsPanel")?.Attributes?["value"]?.Value;
                if (showSettingsPanel != null)
                {
                    view.ShowSettingsPanel = showSettingsPanel.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                var showFaces = viewNode.SelectSingleNode("showFaces")?.Attributes?["value"]?.Value;
                if (showFaces != null)
                {
                    view.ShowFaces = showFaces.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                var showWireframes = viewNode.SelectSingleNode("showWireframes")?.Attributes?["value"]?.Value;
                if (showWireframes != null)
                {
                    view.ShowWireframes = showWireframes.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                var showLines = viewNode.SelectSingleNode("showLines")?.Attributes?["value"]?.Value;
                if (showLines != null)
                {
                    view.ShowLines = showLines.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                var showPoints = viewNode.SelectSingleNode("showPoints")?.Attributes?["value"]?.Value;
                if (showPoints != null)
                {
                    view.ShowPoints = showPoints.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                var showModelCS = viewNode.SelectSingleNode("showModelCS")?.Attributes?["value"]?.Value;
                if (showModelCS != null)
                {
                    view.ShowModelCS = showModelCS.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                var showWorldCS = viewNode.SelectSingleNode("showWorldCS")?.Attributes?["value"]?.Value;
                if (showWorldCS != null)
                {
                    view.ShowWorldCS = showWorldCS.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                var showNavigator = viewNode.SelectSingleNode("showNavigator")?.Attributes?["value"]?.Value;
                if (showNavigator != null)
                {
                    view.ShowNavigator = showNavigator.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                view.BackgroundColor = viewNode.SelectSingleNode("backgroundColor")?.Attributes?["value"]?.Value;
                view.SelectionColor = viewNode.SelectSingleNode("selectionColor")?.Attributes?["value"]?.Value;
                view.Rotation = viewNode.SelectSingleNode("rotation")?.Attributes?["value"]?.Value;
                view.EyeVector = viewNode.SelectSingleNode("eyeVector")?.Attributes?["value"]?.Value;
                view.TargetVector = viewNode.SelectSingleNode("targetVector")?.Attributes?["value"]?.Value;
                view.UpVector = viewNode.SelectSingleNode("upVector")?.Attributes?["value"]?.Value;
            }

            return view;
        }
    }
}
