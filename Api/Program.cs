using Application;
using MPDCApiTemplate.ExceptionHandlers;
using MPDCApiTemplate.OpenApi;
using Infrastructure;
using Infrastructure.Extensions.Cors;
using Infrastructure.Extensions.Logs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Persistence;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
        loggerConfiguration.ConfigureInfraSerilog(context.Configuration),
    writeToProviders: true);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
    .AddInfra(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options => options.AddPolicy("notification-write", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

var app = builder.Build();

app.MapOpenApi().WithMetadata(new AllowAnonymousAttribute());

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "MPDCApiTemplate v1");
});

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(CorsExtension.DefaultPolicyName);

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

// Exposed so Unit Tests can bootstrap this host during tests.
public partial class Program;

