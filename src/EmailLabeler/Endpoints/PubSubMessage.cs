namespace EmailLabeler.Endpoints;

/// <summary>The push notification envelope from Google Cloud Pub/Sub.</summary>
public record PubSubPushEnvelope(PubSubMessagePayload Message, string Subscription);

/// <summary>The Pub/Sub message payload containing base64-encoded data.</summary>
public record PubSubMessagePayload(string Data, string MessageId, string PublishTime);

/// <summary>The decoded data field: { "emailAddress": "...", "historyId": "..." }</summary>
public record GmailNotification(string EmailAddress, ulong HistoryId);
