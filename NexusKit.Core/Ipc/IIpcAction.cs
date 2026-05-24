namespace NexusKit.Core.Ipc;

/// <summary>
/// Typed proxy for a foreign-plugin IPC action (no return value).
/// <see cref="TryInvoke"/> swallows exceptions and reports success/failure.
/// </summary>
public interface IIpcAction
{
    void Invoke();
    bool TryInvoke();
}

public interface IIpcAction<T1>
{
    void Invoke(T1 arg);
    bool TryInvoke(T1 arg);
}

public interface IIpcAction<T1, T2>
{
    void Invoke(T1 arg1, T2 arg2);
    bool TryInvoke(T1 arg1, T2 arg2);
}
