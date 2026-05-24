namespace NexusKit.Ui.Commands;

public delegate void CommandHandler(string command, string arguments);

/// <summary>
/// Slash-command registration with automatic cleanup. Disposing the returned
/// handle removes the command; the registry itself disposes every still-tracked
/// command at plugin shutdown.
/// </summary>
public interface ICommandRegistry
{
    IDisposable Register(string command, CommandHandler handler, string? help = null);
}
