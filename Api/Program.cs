using Application;
using ApiTemplate.ExceptionHandlers;
using Infra;
using Infra.Extensions.Cors;
using Infra.Extensions.Logs;
using Persistence;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
        loggerConfiguration.ConfigureInfraSerilog(context.Configuration),
    writeToProviders: true);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
    .AddInfra(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
}

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "ApiTemplate v1");
});

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(CorsExtension.DefaultPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
