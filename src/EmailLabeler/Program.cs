var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

#pragma warning disable CS1591
public partial class Program { }
#pragma warning restore CS1591
