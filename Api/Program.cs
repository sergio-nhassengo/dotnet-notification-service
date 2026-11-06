using Application;
using Infrastructure.Extensions.Logs;
using Infrastructure;
using MPDCApiTemplate.Authentication;
using MPDCApiTemplate.Cors;
using MPDCApiTemplate.ExceptionHandlers;
using MPDCApiTemplate.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Persistence;
using Serilog.Formatting.Json;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
        loggerConfiguration.ConfigureInfraSerilog(context.Configuration),
    writeToProviders: true);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>());

builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
    .AddInfra(builder.Configuration)
    .AddApiKeyAuthentication()
    .AddApiCors(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}


app.MapOpenApi().WithMetadata(new AllowAnonymousAttribute());
app.MapGet("/", () => Results.Redirect("swagger")).AllowAnonymous().ExcludeFromDescription();

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "MPDC Notification Service API Documentation";
    options.SwaggerEndpoint("/openapi/v1.json", "MPDCApiTemplate v1");
});

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(CorsExtension.DefaultPolicyName);

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

// Exposed so Unit Tests can bootstrap this host during tests.
public partial class Program;
