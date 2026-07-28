using Europeana3D.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Europeana3D.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversionController : ControllerBase
    {
        private readonly ZenodoCsvService _csv;

        public ConversionController(ZenodoCsvService csv) => _csv = csv;

        [HttpGet("display-model")]
        public async Task<IActionResult> DisplayModel([FromQuery] int recordId, [FromQuery] string pid, [FromQuery] string? accessToken = null)
        {
            var csvContent = await _csv.FetchCsvAsync(recordId.ToString(), accessToken);
            if (csvContent == null)
                return NotFound($"Metadata not found in Zenodo record {recordId}.");

            var rows = _csv.ParseCsv(csvContent);
            var row = _csv.FindRowByPid(rows, pid);
            if (row == null)
                return NotFound($"PID '{pid}' not found in metadata.csv.");

            var metadataUrl = Url.Action("ModelMetadata", "Home", new
            {
                zenodoRecordId = recordId.ToString(),
                pid,
                accessToken,
                landingPage = true
            });

            return Redirect(metadataUrl!);
        }
    }
}
