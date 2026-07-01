using Microsoft.AspNetCore.Mvc;
using System.Xml;

namespace XRCultureViewer.Services
{
    public interface IModelLoaderService
    {
        Task<IActionResult> LoadModelXMLAsync(XmlDocument xmlDoc, string serviceRootUrl);
        Task<IActionResult> LoadModelJSONAsync(dynamic jsonDoc, string serviceRootUrl);
    }
}