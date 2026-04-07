namespace EmailLabeler.Domain;

/// <summary>Supported email action types.</summary>
public enum ActionType
{
    /// <summary>Apply a label to the email.</summary>
    Label,

    /// <summary>Archive the email (remove from inbox).</summary>
    Archive
}
