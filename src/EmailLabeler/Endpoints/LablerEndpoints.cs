namespace EmailLabeler.Endpoints;

using System.Text;
using System.Text.Json;
using EmailLabeler.Engine;
using EmailLabeler.Ports;

/// <summary>Maps the labler endpoints for receiving Pub/Sub push notifications.</summary>
public static class LablerEndpoints
{
    /// <summary>Registers the /labler POST endpoint.</summary>
    public static void MapLablerEndpoints(this WebApplication app)
    {
        app.MapPost("/labler", HandleLabler)
            .AddEndpointFilter(async (context, next) =>
            {
                var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var expectedToken = config["PUBSUB_VERIFICATION_TOKEN"];

                if (string.IsNullOrEmpty(expectedToken))
                    return await next(context);

                var authHeader = context.HttpContext.Request.Headers.Authorization.FirstOrDefault();
                if (authHeader is null || !authHeader.StartsWith("Bearer "))
                    return Results.Unauthorized();

                var token = authHeader["Bearer ".Length..];
                if (token != expectedToken)
                    return Results.Unauthorized();

                return await next(context);
            });
    }

    private static async Task<IResult> HandleLabler(
        PubSubPushEnvelope envelope,
        IEmailRepository repo,
        EmailProcessor processor,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("EmailLabeler.Endpoints.LablerEndpoints");

        GmailNotification? notification;
        try
        {
            var dataBytes = Convert.FromBase64String(envelope.Message.Data);
            var dataJson = Encoding.UTF8.GetString(dataBytes);
            notification = JsonSerializer.Deserialize<GmailNotification>(
                dataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            logger.LogWarning(ex, "Failed to decode Pub/Sub notification data");
            return Results.BadRequest();
        }

        if (notification is null)
        {
            logger.LogWarning("Failed to deserialize Pub/Sub notification data");
            return Results.BadRequest();
        }

        var messageIds = await repo.GetNewMessageIdsAsync(notification.HistoryId);

        foreach (var messageId in messageIds)
        {
            await processor.ProcessAsync(messageId);
        }

        return Results.Ok();
    }
}
