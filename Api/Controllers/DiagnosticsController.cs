using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MPDCApiTemplate.Controllers;

[ApiController]
[Route("diagnostics")]
public class DiagnosticsController : ControllerBase
{
    [HttpGet("throw-exception")]
    [AllowAnonymous]
    public IActionResult ThrowException()
    {
        throw new InvalidOperationException("This is a test exception thrown on purpose to verify OpenTelemetry traces, logs, and metrics show up in Grafana.");
    }
}
