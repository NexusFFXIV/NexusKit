using System.Numerics;

namespace NexusKit.Core.Maps;

/// <summary>
/// A resolved point on an in-game map, in both coordinate systems the UI needs.
/// <para><see cref="MapX"/>/<see cref="MapY"/> are the human-readable coordinates
/// shown in the game's own map UI and chat map links (the "(12.3, 8.7)" form) —
/// use these for display. <see cref="World"/> is the untranslated engine
/// position, kept so callers that need real distances or 3D math don't have to
/// resolve the object a second time. Note the axis remap: the map's vertical
/// axis is the world's <c>Z</c>, and the world's <c>Y</c> is elevation.</para>
/// </summary>
/// <param name="TerritoryId">Row id in the <c>TerritoryType</c> sheet, narrowed to
/// <c>ushort</c> to match the territory lookups throughout the framework.</param>
/// <param name="MapId">Row id in the <c>Map</c> sheet — the map the player is
/// actually looking at, which in multi-map territories is not the territory's
/// default map.</param>
/// <param name="MapX">Human-readable map x-coordinate.</param>
/// <param name="MapY">Human-readable map y-coordinate (derived from world Z).</param>
/// <param name="World">Raw world-space position; <c>Y</c> is elevation.</param>
public sealed record MapPosition(
    ushort TerritoryId,
    uint MapId,
    float MapX,
    float MapY,
    Vector3 World);
