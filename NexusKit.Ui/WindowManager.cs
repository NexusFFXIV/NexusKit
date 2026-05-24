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
}
