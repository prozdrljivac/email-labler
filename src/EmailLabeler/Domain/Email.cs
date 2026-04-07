namespace EmailLabeler.Domain;

/// <summary>A provider-agnostic representation of an email message.</summary>
public record Email(string Id, string From, string Subject, string[] LabelIds);
