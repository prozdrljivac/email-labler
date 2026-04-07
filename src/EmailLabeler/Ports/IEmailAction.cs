namespace EmailLabeler.Ports;

using EmailLabeler.Domain;

/// <summary>Strategy interface for executing an action on an email.</summary>
public interface IEmailAction
{
    /// <summary>The action type this handler supports.</summary>
    ActionType Type { get; }

    /// <summary>Executes the action on the given email.</summary>
    Task ExecuteAsync(Email email, ActionConfig config, IEmailRepository repo);
}
