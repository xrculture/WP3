using System.Xml.Linq;

namespace Europeana3D.Web.Services
{
    public class XmlTemplateService
    {
        private readonly IWebHostEnvironment _env;
        public XmlTemplateService(IWebHostEnvironment env) => _env = env;


        public XDocument LoadViewersXml()
        {
            var path = Path.Combine(_env.ContentRootPath, "Resources", "Viewers.xml");
            return XDocument.Load(path);
        }


        public XDocument LoadModelLoadingTemplate()
        {
            var path = Path.Combine(_env.ContentRootPath, "Resources", "ModelLoading.xml");
            return XDocument.Load(path);
        }

        public XDocument LoadModelRequestTemplate()
        {
            var path = Path.Combine(_env.ContentRootPath, "Resources", "ModelRequest.xml");
            return XDocument.Load(path);
        }

        public XDocument LoadRepositoriesXml()
        {
            var path = Path.Combine(_env.ContentRootPath, "Resources", "Repositories.xml");
            return XDocument.Load(path);
        }

        public void SaveLocalXML(XDocument tmpl, string resourcesDir, string suffix)
        {
            try
            {
                Directory.CreateDirectory(resourcesDir);
                var fileName = $"{suffix}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.xml";
                var outPath = Path.Combine(resourcesDir, fileName);
                using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    tmpl.Save(fs);
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: continue with HTTP call
                Console.WriteLine($"[ViewerService] Failed to save {suffix} XML: {ex.Message}");
            }
        }
    }
}
