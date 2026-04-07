using EmailLabeler.Actions;
using EmailLabeler.Configuration;
using EmailLabeler.Engine;
using EmailLabeler.Ports;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddYamlFile("config.yaml", optional: false, reloadOnChange: true);
builder.Services.AddRulesConfig(builder.Configuration);

builder.Services.AddSingleton<IEmailAction, LabelAction>();
builder.Services.AddSingleton<IEmailAction, ArchiveAction>();
builder.Services.AddScoped<EmailProcessor>();

builder.Services.AddGmailIntegration();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

#pragma warning disable CS1591
public partial class Program { }
#pragma warning restore CS1591
