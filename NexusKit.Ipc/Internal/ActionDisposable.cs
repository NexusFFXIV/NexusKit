namespace NexusKit.Ipc.Internal;

internal sealed class ActionDisposable : IDisposable
{
    private Action? mAction;

    public ActionDisposable(Action action)
    {
        mAction = action;
    }

    public void Dispose()
    {
        var a = Interlocked.Exchange(ref mAction, null);
        a?.Invoke();
    }
}
