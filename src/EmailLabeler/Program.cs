using EmailLabeler.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddYamlFile("config.yaml", optional: false, reloadOnChange: true);
builder.Services.AddRulesConfig(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

#pragma warning disable CS1591
public partial class Program { }
#pragma warning restore CS1591
