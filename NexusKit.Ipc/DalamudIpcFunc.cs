using Dalamud.Plugin.Ipc;
using NexusKit.Core.Ipc;

namespace NexusKit.Ipc;

internal sealed class DalamudIpcFunc<TResult> : IIpcFunc<TResult>
{
    private readonly ICallGateSubscriber<TResult> mSubscriber;

    public DalamudIpcFunc(ICallGateSubscriber<TResult> subscriber)
    {
        mSubscriber = subscriber;
    }

    public TResult Invoke() => mSubscriber.InvokeFunc();

    public bool TryInvoke(out TResult result)
    {
        try
        {
            result = mSubscriber.InvokeFunc();
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }
}

internal sealed class DalamudIpcFunc<T1, TResult> : IIpcFunc<T1, TResult>
{
    private readonly ICallGateSubscriber<T1, TResult> mSubscriber;

    public DalamudIpcFunc(ICallGateSubscriber<T1, TResult> subscriber)
    {
        mSubscriber = subscriber;
    }

    public TResult Invoke(T1 arg) => mSubscriber.InvokeFunc(arg);

    public bool TryInvoke(T1 arg, out TResult result)
    {
        try
        {
            result = mSubscriber.InvokeFunc(arg);
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }
}

internal sealed class DalamudIpcFunc<T1, T2, TResult> : IIpcFunc<T1, T2, TResult>
{
    private readonly ICallGateSubscriber<T1, T2, TResult> mSubscriber;

    public DalamudIpcFunc(ICallGateSubscriber<T1, T2, TResult> subscriber)
    {
        mSubscriber = subscriber;
    }

    public TResult Invoke(T1 arg1, T2 arg2) => mSubscriber.InvokeFunc(arg1, arg2);

    public bool TryInvoke(T1 arg1, T2 arg2, out TResult result)
    {
        try
        {
            result = mSubscriber.InvokeFunc(arg1, arg2);
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }
}
