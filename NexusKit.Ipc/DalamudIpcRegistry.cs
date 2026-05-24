using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using NexusKit.Core.Context;
using NexusKit.Core.Ipc;
using NexusKit.Ipc.Internal;

namespace NexusKit.Ipc;

internal sealed class DalamudIpcRegistry : IIpcRegistry, IDisposable
{
    private readonly IDalamudPluginInterface mPluginInterface;
    private readonly IPluginContext mContext;
    private readonly List<IDisposable> mOwnRegistrations = new();

    public DalamudIpcRegistry(IDalamudPluginInterface pluginInterface, IPluginContext context)
    {
        mPluginInterface = pluginInterface;
        mContext = context;
    }

    public string BuildName(string subsystem, string member)
        => $"{mContext.PluginName}.{subsystem}.{member}";

    // --- Provider: Func ----------------------------------------------------

    public IDisposable RegisterFunc<TResult>(string subsystem, string function, Func<TResult> handler)
    {
        var name = BuildName(subsystem, function);
        var provider = mPluginInterface.GetIpcProvider<TResult>(name);
        provider.RegisterFunc(handler);
        return Track(() => SafeUnregisterFunc(provider));
    }

    public IDisposable RegisterFunc<T1, TResult>(string subsystem, string function, Func<T1, TResult> handler)
    {
        var name = BuildName(subsystem, function);
        var provider = mPluginInterface.GetIpcProvider<T1, TResult>(name);
        provider.RegisterFunc(handler);
        return Track(() => SafeUnregisterFunc(provider));
    }

    public IDisposable RegisterFunc<T1, T2, TResult>(string subsystem, string function, Func<T1, T2, TResult> handler)
    {
        var name = BuildName(subsystem, function);
        var provider = mPluginInterface.GetIpcProvider<T1, T2, TResult>(name);
        provider.RegisterFunc(handler);
        return Track(() => SafeUnregisterFunc(provider));
    }

    // --- Provider: Action --------------------------------------------------

    public IDisposable RegisterAction(string subsystem, string action, Action handler)
    {
        var name = BuildName(subsystem, action);
        var provider = mPluginInterface.GetIpcProvider<object>(name);
        provider.RegisterAction(handler);
        return Track(() => SafeUnregisterAction(provider));
    }

    public IDisposable RegisterAction<T1>(string subsystem, string action, Action<T1> handler)
    {
        var name = BuildName(subsystem, action);
        var provider = mPluginInterface.GetIpcProvider<T1, object>(name);
        provider.RegisterAction(handler);
        return Track(() => SafeUnregisterAction(provider));
    }

    public IDisposable RegisterAction<T1, T2>(string subsystem, string action, Action<T1, T2> handler)
    {
        var name = BuildName(subsystem, action);
        var provider = mPluginInterface.GetIpcProvider<T1, T2, object>(name);
        provider.RegisterAction(handler);
        return Track(() => SafeUnregisterAction(provider));
    }

    // --- Provider: SendMessage ---------------------------------------------

    public void Publish(string subsystem, string eventName)
    {
        var name = BuildName(subsystem, eventName);
        var provider = mPluginInterface.GetIpcProvider<object>(name);
        provider.SendMessage();
    }

    public void Publish<T1>(string subsystem, string eventName, T1 arg)
    {
        var name = BuildName(subsystem, eventName);
        var provider = mPluginInterface.GetIpcProvider<T1, object>(name);
        provider.SendMessage(arg);
    }

    // --- Consumer: Func ----------------------------------------------------

    public IIpcFunc<TResult> GetFunc<TResult>(string fullName)
        => new DalamudIpcFunc<TResult>(mPluginInterface.GetIpcSubscriber<TResult>(fullName));

    public IIpcFunc<T1, TResult> GetFunc<T1, TResult>(string fullName)
        => new DalamudIpcFunc<T1, TResult>(mPluginInterface.GetIpcSubscriber<T1, TResult>(fullName));

    public IIpcFunc<T1, T2, TResult> GetFunc<T1, T2, TResult>(string fullName)
        => new DalamudIpcFunc<T1, T2, TResult>(mPluginInterface.GetIpcSubscriber<T1, T2, TResult>(fullName));

    // --- Consumer: Action --------------------------------------------------

    public IIpcAction GetAction(string fullName)
        => new DalamudIpcAction(mPluginInterface.GetIpcSubscriber<object>(fullName));

    public IIpcAction<T1> GetAction<T1>(string fullName)
        => new DalamudIpcAction<T1>(mPluginInterface.GetIpcSubscriber<T1, object>(fullName));

    public IIpcAction<T1, T2> GetAction<T1, T2>(string fullName)
        => new DalamudIpcAction<T1, T2>(mPluginInterface.GetIpcSubscriber<T1, T2, object>(fullName));

    // --- Consumer: Subscribe to events -------------------------------------

    public IDisposable Subscribe(string fullName, Action handler)
    {
        var subscriber = mPluginInterface.GetIpcSubscriber<object>(fullName);
        subscriber.Subscribe(handler);
        return new ActionDisposable(() => SafeUnsubscribe(() => subscriber.Unsubscribe(handler)));
    }

    public IDisposable Subscribe<T1>(string fullName, Action<T1> handler)
    {
        var subscriber = mPluginInterface.GetIpcSubscriber<T1, object>(fullName);
        subscriber.Subscribe(handler);
        return new ActionDisposable(() => SafeUnsubscribe(() => subscriber.Unsubscribe(handler)));
    }

    public void Dispose()
    {
        foreach (var r in mOwnRegistrations)
        {
            try { r.Dispose(); } catch { /* shutdown */ }
        }
        mOwnRegistrations.Clear();
    }

    private IDisposable Track(Action unregister)
    {
        var d = new ActionDisposable(unregister);
        mOwnRegistrations.Add(d);
        return d;
    }

    private static void SafeUnregisterFunc(ICallGateProvider provider)
    {
        try { provider.UnregisterFunc(); } catch { /* shutdown */ }
    }

    private static void SafeUnregisterAction(ICallGateProvider provider)
    {
        try { provider.UnregisterAction(); } catch { /* shutdown */ }
    }

    private static void SafeUnsubscribe(Action unsubscribe)
    {
        try { unsubscribe(); } catch { /* shutdown */ }
    }
}
