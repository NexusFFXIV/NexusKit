using System.Numerics;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using NexusKit.Core.Maps;
using NexusKit.GameData.ObjectTables;

namespace NexusKit.GameData.Maps;

/// <summary>
/// Dalamud-side <see cref="IPlayerMapMarker"/> implementation. Resolves the
/// target's live world position from the object table and hands it to the game's
/// own map-link machinery, so the resulting flag behaves exactly like one placed
/// by clicking a map link in chat (map window, minimap and compass all follow).
/// <para>Deliberately silent on failure: every "can't resolve" case is a normal
/// UI state (target out of range, loading screen, map-less territory) that the
/// caller renders as a disabled control, and these methods run on the render
/// path where logging would spam per frame.</para>
/// </summary>
internal sealed class DalamudPlayerMapMarker : IPlayerMapMarker
{
    private readonly IObjectTable mObjectTable;
    private readonly IClientState mClientState;
    private readonly IGameGui mGameGui;
    private readonly ISheetsProvider mSheets;

    public DalamudPlayerMapMarker(IObjectTable objectTable, IClientState clientState,
                                  IGameGui gameGui, ISheetsProvider sheets)
    {
        mObjectTable = objectTable;
        mClientState = clientState;
        mGameGui = gameGui;
        mSheets = sheets;
    }

    public MapPosition? TryGetPosition(ulong contentId)
    {
        if (!TryResolve(contentId, out var territoryId, out var map, out var world))
            return null;

        // WorldToMap applies the map's SizeFactor and X/Y offsets; feeding it
        // (world.X, world.Z) is the axis remap the game's map UI uses — world Y
        // is elevation and has no place on a 2D map.
        var onMap = MapUtil.WorldToMap(new Vector2(world.X, world.Z), map);
        return new MapPosition(territoryId, map.RowId, onMap.X, onMap.Y, world);
    }

    public bool MarkPosition(ulong contentId)
    {
        if (!TryResolve(contentId, out var territoryId, out var map, out var world))
            return false;

        // Raw ctor rather than the IGameGui.OpenMapWithMapLink(territory, map,
        // worldPos) overload: that one casts each world coordinate to int BEFORE
        // scaling by 1000, snapping the flag to the nearest whole game unit.
        var payload = new MapLinkPayload(territoryId, map.RowId,
            rawX: (int)(world.X * 1000f),
            rawY: (int)(world.Z * 1000f));
        return mGameGui.OpenMapWithMapLink(payload);
    }

    /// <summary>Shared resolve step: current territory/map plus the target's live
    /// position. False means "nothing to mark" for any of the expected reasons.</summary>
    private bool TryResolve(ulong contentId, out ushort territoryId, out Map map, out Vector3 world)
    {
        territoryId = 0;
        map = default;
        world = default;

        // IClientState exposes uint TerritoryType; narrowed to ushort to match the
        // Lumina sheet's key width, as everywhere else in the framework.
        //
        // Both ids are 0 outside of gameplay (login, loading screen), and MapId
        // stays 0 in the few territories that have no map at all. Reading MapId
        // rather than resolving TerritoryType.Map matters in zones that swap maps
        // underneath one territory — housing districts, the Firmament — where the
        // territory's default map would put the flag on the wrong sheet.
        territoryId = (ushort)mClientState.TerritoryType;
        var mapId = mClientState.MapId;
        if (territoryId == 0 || mapId == 0) return false;

        if (mSheets.GetSheet<Map>()?.GetRowOrDefault(mapId) is not { } mapRow) return false;

        // Only players in the object table have a position — this is what makes
        // "out of range" indistinguishable from "unknown player", by design.
        var pc = mObjectTable.FindPlayerCharacter(contentId);
        if (pc is null) return false;

        map = mapRow;
        world = pc.Position;
        return true;
    }
}
