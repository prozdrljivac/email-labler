namespace EmailLabeler.Health;

using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>Writes a compact JSON body summarizing the overall status and each individual check.</summary>
public static class HealthResponseWriter
{
    /// <summary>Serializes <paramref name="report"/> as JSON to the HTTP response.</summary>
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                error = e.Value.Exception?.Message,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
