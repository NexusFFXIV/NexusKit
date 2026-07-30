namespace NexusKit.Core.Maps;

/// <summary>
/// Reads the live map position of a nearby player and drops the game's flag
/// marker on it. Dalamud-free surface so views can offer a "mark on map" action
/// without touching <c>IObjectTable</c>, <c>IGameGui</c> or Lumina themselves
/// (registered by <c>AddNexusKitGameData</c>, same pattern as
/// <see cref="ILocalPlayerContext"/>).
/// <para><b>Range:</b> only players the local character can currently see are
/// resolvable — the game only exposes positions for objects in the object table.
/// Both members return <c>null</c>/<c>false</c> for anyone else, so callers can
/// use them directly to gate a button rather than tracking visibility
/// separately.</para>
/// <para><b>Threading:</b> callers MUST invoke from the framework thread (any
/// ImGui callback, <c>IFramework.Update</c> handler, or
/// <c>IFramework.RunOnFrameworkThread</c> body satisfies that).</para>
/// </summary>
public interface IPlayerMapMarker
{
    /// <summary>Resolve where the player with <paramref name="contentId"/> is
    /// standing right now. <c>null</c> when they aren't in range, when not
    /// in-game, or when the current territory has no map (cutscene and other
    /// special zones).</summary>
    MapPosition? TryGetPosition(ulong contentId);

    /// <summary>Place the in-game flag marker on the player's current position
    /// and open the map there. Returns <c>false</c> — without side effects —
    /// when the position could not be resolved, which includes the case where
    /// the player walked out of range since the UI last checked.</summary>
    bool MarkPosition(ulong contentId);
}
