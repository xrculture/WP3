using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace XRCultureHub.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void OnGet()
        {
        }

        public List<ViewerDescriptor> GetViewers()
        {
            return ViewersRegistry.GetViewerDescriptors(_logger, _configuration);
        }

        public List<ServiceDescriptor> GetServices()
        {
            return ServicesRegistry.GetServiceDescriptors(_logger, _configuration);
        }
    }
}
