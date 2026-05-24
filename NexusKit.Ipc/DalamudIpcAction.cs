using Dalamud.Plugin.Ipc;
using NexusKit.Core.Ipc;

namespace NexusKit.Ipc;

internal sealed class DalamudIpcAction : IIpcAction
{
    private readonly ICallGateSubscriber<object> mSubscriber;

    public DalamudIpcAction(ICallGateSubscriber<object> subscriber)
    {
        mSubscriber = subscriber;
    }

    public void Invoke() => mSubscriber.InvokeAction();

    public bool TryInvoke()
    {
        try { mSubscriber.InvokeAction(); return true; }
        catch { return false; }
    }
}

internal sealed class DalamudIpcAction<T1> : IIpcAction<T1>
{
    private readonly ICallGateSubscriber<T1, object> mSubscriber;

    public DalamudIpcAction(ICallGateSubscriber<T1, object> subscriber)
    {
        mSubscriber = subscriber;
    }

    public void Invoke(T1 arg) => mSubscriber.InvokeAction(arg);

    public bool TryInvoke(T1 arg)
    {
        try { mSubscriber.InvokeAction(arg); return true; }
        catch { return false; }
    }
}

internal sealed class DalamudIpcAction<T1, T2> : IIpcAction<T1, T2>
{
    private readonly ICallGateSubscriber<T1, T2, object> mSubscriber;

    public DalamudIpcAction(ICallGateSubscriber<T1, T2, object> subscriber)
    {
        mSubscriber = subscriber;
    }

    public void Invoke(T1 arg1, T2 arg2) => mSubscriber.InvokeAction(arg1, arg2);

    public bool TryInvoke(T1 arg1, T2 arg2)
    {
        try { mSubscriber.InvokeAction(arg1, arg2); return true; }
        catch { return false; }
    }
}
