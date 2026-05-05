using EmailLabeler.Actions;
using EmailLabeler.Configuration;
using EmailLabeler.Engine;
using EmailLabeler.Ports;
using EmailLabeler.Services;
using EmailLabeler.Endpoints;

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

builder.Services.AddGmailIntegration();
builder.Services.AddSingleton<IPubSubTokenValidator, PubSubTokenValidator>();
builder.Services.AddHostedService<WatchRenewalService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapLablerEndpoints();

app.Run();

#pragma warning disable CS1591
public partial class Program { }
#pragma warning restore CS1591
