using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexusKit.Core;
using NexusKit.Core.Localization;
using NexusKit.Core.Maps;
using NexusKit.GameData.Maps;

namespace NexusKit.GameData;

public static class GameDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the GameData services as singletons. The Dalamud handles they
    /// depend on must already be registered by the plugin host: <c>IDataManager</c>
    /// (SheetsProvider), <c>IObjectTable</c>, <c>IClientState</c> and
    /// <c>IGameGui</c> (local-player context and map markers) — all taken via
    /// constructor injection.
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

        // Map markers — same rationale, and additionally keeps IGameGui out of
        // view code. Requires the host to have registered IGameGui.
        services.AddSingleton<IPlayerMapMarker, DalamudPlayerMapMarker>();

        return services;
    }
}
