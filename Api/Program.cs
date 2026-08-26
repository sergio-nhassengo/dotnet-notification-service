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

app.MapControllers();

app.Run();

// Exposed so Unit Tests can bootstrap this host during tests.
public partial class Program;
