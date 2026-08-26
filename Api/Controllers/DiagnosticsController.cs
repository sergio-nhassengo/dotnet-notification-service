using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace MPDCApiTemplate.Controllers;

[ApiController]
[Route("diagnostics")]
public class DiagnosticsController(IHostEnvironment environment) : ControllerBase
{
    [HttpGet("throw-exception")]
    [AllowAnonymous]
    public IActionResult ThrowException()
    {
        // Only usable outside Development if someone flips ASPNETCORE_ENVIRONMENT - this endpoint
        // exists to exercise the telemetry pipeline locally, not to be a public 500-on-demand switch.
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        throw new InvalidOperationException("This is a test exception thrown on purpose to verify OpenTelemetry traces, logs, and metrics show up in Grafana.");
    }
}
