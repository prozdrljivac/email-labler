using EmailLabeler.Actions;
using EmailLabeler.Configuration;
using EmailLabeler.Endpoints;
using EmailLabeler.Engine;
using EmailLabeler.Health;
using EmailLabeler.Ports;
using EmailLabeler.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";
    options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
});

builder.Configuration.AddYamlFile("config.yaml", optional: false, reloadOnChange: true);
builder.Services.AddRulesConfig(builder.Configuration);

builder.Services.AddSingleton<IEmailAction, LabelAction>();
builder.Services.AddSingleton<IEmailAction, ArchiveAction>();
builder.Services.AddScoped<EmailProcessor>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WatchRenewalState>();

builder.Services.AddGmailIntegration();
builder.Services.AddSingleton<IPubSubTokenValidator, PubSubTokenValidator>();
builder.Services.AddHostedService<WatchRenewalService>();

builder.Services.AddHealthChecks()
    .AddCheck<WatchRenewalHealthCheck>("watch-renewal");

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});
app.MapLablerEndpoints();

app.Run();

#pragma warning disable CS1591
public partial class Program { }
#pragma warning restore CS1591
