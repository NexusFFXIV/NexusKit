using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using NexusKit.Ui.Abstractions;

namespace NexusKit.Ui;

internal sealed class WindowManager : IWindowManager
{
    private readonly IServiceProvider mServices;

    public WindowManager(IServiceProvider services)
    {
        mServices = services;
    }

    public void OpenMain()
    {
        if (mServices.GetService<MainWindow>() is { } w) w.IsOpen = true;
    }

    public void OpenSettings()
    {
        if (mServices.GetService<SettingsWindow>() is { } w) w.IsOpen = true;
    }

    public void Open<T>() where T : NexusWindow
    {
        if (mServices.GetService<T>() is { } w) w.IsOpen = true;
    }

    public void Close<T>() where T : NexusWindow
    {
        if (mServices.GetService<T>() is { } w) w.IsOpen = false;
    }

    public void Toggle<T>() where T : NexusWindow
    {
        if (mServices.GetService<T>() is { } w) w.IsOpen = !w.IsOpen;
    }

    public bool IsOpen<T>() where T : NexusWindow
        => mServices.GetService<T>() is { } w && w.IsOpen;

    public T? Get<T>() where T : NexusWindow
        => mServices.GetService<T>();

    public void Invoke<T>(Action<T> action) where T : NexusWindow
    {
        if (mServices.GetService<T>() is not { } w) return;

        // Window mutations must land on the framework thread (that's where the
        // UI reads them). RunOnFrameworkThread runs inline when already on it,
        // so on-thread callers pay nothing. Fall back to a direct call if no
        // framework service is registered (e.g. headless test hosts).
        if (mServices.GetService<IFramework>() is { } framework)
            framework.RunOnFrameworkThread(() => action(w));
        else
            action(w);
    }
}
