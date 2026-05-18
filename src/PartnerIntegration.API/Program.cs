using Serilog;
using Serilog.Events;
using PartnerIntegration.API.Extensions;
using PartnerIntegration.API.Middleware;
using PartnerIntegration.Infrastructure;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/partner-integration-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Starting Partner Integration BFF");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ─── Services ─────────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerWithApiKey();
    builder.Services.AddResponseCompression();

    // Application layer
    builder.Services.AddApplication();

    // Infrastructure layer (RabbitMQ + Polly HTTP Client)
    builder.Services.AddInfrastructure(builder.Configuration);

    // API Key authentication
    builder.Services.Configure<ApiKeySettings>(
        builder.Configuration.GetSection(ApiKeySettings.SectionName));

    // Health checks
    builder.Services.AddHealthChecks();

    // ─── Pipeline ─────────────────────────────────────────────────────────────
    var app = builder.Build();

    // Global exception handler (MUST be first middleware)
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Partner Integration BFF v1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    app.UseHttpsRedirection();

    // API Key authentication middleware
    app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for integration tests
public partial class Program { }
