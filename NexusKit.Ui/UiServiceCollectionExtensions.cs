using Dalamud.Interface.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexusKit.Core;
using NexusKit.Core.Context;
using NexusKit.Core.Localization;
using NexusKit.Core.Utilities;
using NexusKit.Persistence.Maintenance;
using NexusKit.Ui.Abstractions;
using NexusKit.Ui.AutoSettings;
using NexusKit.Ui.AutoSettings.Maintenance;
using NexusKit.Ui.Commands;
using NexusKit.Ui.Imaging;
using NexusKit.Ui.Resources;
using NexusKit.Ui.Utilities;

namespace NexusKit.Ui;

public static class UiServiceCollectionExtensions
{
    public static IServiceCollection AddNexusKitUi(this IServiceCollection services)
    {
        services.AddSingleton<WindowSystem>(sp =>
        {
            var ctx = sp.GetRequiredService<IPluginContext>();
            return new WindowSystem(ctx.PluginName);
        });
        services.AddSingleton<PluginUiHost>();
        services.AddSingleton<IWindowManager, WindowManager>();
        services.AddSingleton<IImageCache, ImageCache>();

        services.AddSingleton<LocalizationManager>();

        // Small utility services. Each takes a Dalamud handle
        // (ICommandManager, IDalamudPluginInterface), which the plugin must
        // register in ConfigureServices. Lazy by default — only constructed on
        // first resolve.
        services.AddSingleton<IBrowserLauncher, DalamudBrowserLauncher>();
        services.AddSingleton<ICommandRegistry, CommandRegistry>();

        services.AddResourceLocalizer<Framework>();
        // Chain Core's localizer (relative-time keys consumed by
        // LocalizerExtensions). Core's Strings resource class is internal,
        // so we go through its public entry point instead of referencing
        // the type directly. Every plugin that uses the framework calls
        // AddNexusKitUi, so the time keys are guaranteed available
        // wherever FormatRelativeTime / FormatTimeSpan is used.
        services.AddNexusKitCore();
        services.TryAddSingleton<ILocalizer, LayeredLocalizer>();

        return services;
    }

    public static IServiceCollection AddMainWindow<T>(this IServiceCollection services)
        where T : MainWindow
    {
        services.AddSingleton<T>();
        services.AddSingleton<MainWindow>(sp => sp.GetRequiredService<T>());
        services.AddSingleton<NexusWindow>(sp => sp.GetRequiredService<T>());
        return services;
    }

    public static IServiceCollection AddSettingsWindow<T>(this IServiceCollection services)
        where T : SettingsWindow
    {
        services.AddSingleton<T>();
        services.AddSingleton<SettingsWindow>(sp => sp.GetRequiredService<T>());
        services.AddSingleton<NexusWindow>(sp => sp.GetRequiredService<T>());
        return services;
    }

    public static IServiceCollection AddAutoSettingsWindow(this IServiceCollection services)
    {
        services.AddSingleton<AutoSettingsWindow>();
        services.AddSingleton<SettingsWindow>(sp => sp.GetRequiredService<AutoSettingsWindow>());
        services.AddSingleton<NexusWindow>(sp => sp.GetRequiredService<AutoSettingsWindow>());
        return services;
    }

    /// <summary>
    /// Register an additional plugin-defined window beyond main and settings. Resolved
    /// directly by type for injection (e.g. into the main window to toggle visibility)
    /// and also via <see cref="NexusWindow"/> so it joins the window system.
    /// </summary>
    public static IServiceCollection AddWindow<T>(this IServiceCollection services)
        where T : NexusWindow
    {
        services.AddSingleton<T>();
        services.AddSingleton<NexusWindow>(sp => sp.GetRequiredService<T>());
        return services;
    }

    /// <summary>
    /// Register the framework-provided DB-maintenance settings section: shows
    /// on-disk + per-table stats, last-run per maintenance contributor, and a
    /// force-run button that bypasses the per-task interval gates. Requires
    /// <c>AddNexusKitPersistence()</c> (for <see cref="IDbStatsService"/>
    /// and <see cref="IDbMaintenanceService"/>).
    /// <para>The optional <paramref name="order"/> places the section in the
    /// settings nav — default 200 puts it after most plugin-specific
    /// sections; pass a smaller value to surface it higher.</para>
    /// </summary>
    public static IServiceCollection AddDbMaintenanceSettingsSection(
        this IServiceCollection services, int order = 200)
    {
        services.AddSingleton<IAutoSettingsSection>(sp =>
            new DbMaintenanceSettingsSection(
                sp.GetRequiredService<IDbStatsService>(),
                sp.GetRequiredService<IDbMaintenanceService>(),
                sp.GetRequiredService<ILocalizer>(),
                order));
        return services;
    }
}
