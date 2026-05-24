using Dalamud.Game.Command;
using Dalamud.Plugin.Services;

namespace NexusKit.Ui.Commands;

internal sealed class CommandRegistry : ICommandRegistry, IDisposable
{
    private readonly ICommandManager mCommandManager;
    private readonly List<string> mRegistered = new();

    public CommandRegistry(ICommandManager commandManager)
    {
        mCommandManager = commandManager;
    }

    public IDisposable Register(string command, CommandHandler handler, string? help = null)
    {
        var info = new CommandInfo((cmd, args) => handler(cmd, args))
        {
            HelpMessage = help ?? string.Empty,
            ShowInHelp = !string.IsNullOrEmpty(help),
        };
        mCommandManager.AddHandler(command, info);
        mRegistered.Add(command);

        var disposed = false;
        return new Disposable(() =>
        {
            if (disposed) return;
            disposed = true;
            try { mCommandManager.RemoveHandler(command); } catch { /* shutdown */ }
            mRegistered.Remove(command);
        });
    }

    public void Dispose()
    {
        foreach (var cmd in mRegistered.ToList())
        {
            try { mCommandManager.RemoveHandler(cmd); } catch { /* shutdown */ }
        }
        mRegistered.Clear();
    }

    private sealed class Disposable : IDisposable
    {
        private Action? mAction;
        public Disposable(Action a) { mAction = a; }
        public void Dispose() => Interlocked.Exchange(ref mAction, null)?.Invoke();
    }
}
