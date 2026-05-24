using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexusKit.Core;
using NexusKit.Core.Localization;

namespace NexusKit.GameData;

public static class GameDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the three GameData services as singletons. <c>IDataManager</c> must
    /// already be registered by the plugin host (Dalamud-side) — SheetsProvider takes
    /// it via constructor injection.
    /// </summary>
    public static IServiceCollection AddNexusKitGameData(this IServiceCollection services)
    {
        services.TryAddSingleton<LocalizationManager>();

        services.AddSingleton<ISheetsProvider, SheetsProvider>();
        services.AddSingleton<IGameDataLookups, GameDataLookups>();
        services.AddSingleton<IGameDataResolver, GameDataResolver>();

        // Local-player context — Dalamud-side ObjectTable lookup hidden behind
        // a Core abstraction so views don't have to know about IObjectTable.
        services.AddSingleton<ILocalPlayerContext, DalamudLocalPlayerContext>();

        return services;
    }
}
