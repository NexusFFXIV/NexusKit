namespace NexusKit.Core.Ipc;

/// <summary>
/// Typed proxy for a foreign-plugin IPC function. <see cref="TryInvoke"/> swallows
/// exceptions (e.g. plugin not installed) and returns <c>false</c>.
/// </summary>
public interface IIpcFunc<TResult>
{
    TResult Invoke();
    bool TryInvoke(out TResult result);
}

public interface IIpcFunc<T1, TResult>
{
    TResult Invoke(T1 arg);
    bool TryInvoke(T1 arg, out TResult result);
}

public interface IIpcFunc<T1, T2, TResult>
{
    TResult Invoke(T1 arg1, T2 arg2);
    bool TryInvoke(T1 arg1, T2 arg2, out TResult result);
}
