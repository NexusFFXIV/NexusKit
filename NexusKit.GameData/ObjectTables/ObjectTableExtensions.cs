using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using NativeChar = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace NexusKit.GameData.ObjectTables;

/// <summary>
/// Cross-cutting extensions over <see cref="IObjectTable"/>. Iterate the table
/// once, filter to valid <see cref="IPlayerCharacter"/> entries, and hand callers
/// an immutable snapshot they can use off the framework thread.
/// <para><b>Threading:</b> every method here MUST be invoked on the framework
/// thread. The <c>IObjectTable</c> is owned by Dalamud's game loop — reading it
/// from another thread can race with the engine's own writes. Either subscribe
/// to <c>IFramework.Update</c> (already on the framework thread) or wrap the
/// call in <c>IFramework.RunOnFrameworkThread(...)</c>.</para>
/// </summary>
public static class ObjectTableExtensions
{
    /// <summary>Snapshot the local player at index 0, or <c>null</c> when the
    /// game isn't loaded / character not yet ready. Index 0 is always reserved
    /// for the local player</summary>
    public static VisiblePlayer? GetSelf(this IObjectTable objectTable)
    {
        var obj = objectTable[0];
        if (obj is not IPlayerCharacter pc) return null;
        var snapshot = ToVisiblePlayer(pc);
        return IsValid(snapshot) ? snapshot : null;
    }

    /// <summary>Snapshot every valid player character currently in the table,
    /// excluding the local player at index 0. Returns an empty enumerable when
    /// not in game or the table is otherwise empty.</summary>
    public static IEnumerable<VisiblePlayer> GetVisiblePlayers(this IObjectTable objectTable)
    {
        // Index 0 is reserved for the local player — skip it via offset, no
        // address comparison needed.
        foreach (var obj in objectTable.Skip(1))
        {
            if (obj.ObjectKind != ObjectKind.Pc) continue;
            if (obj is not IPlayerCharacter pc) continue;
            var snapshot = ToVisiblePlayer(pc);
            if (!IsValid(snapshot)) continue;
            yield return snapshot;
        }
    }

    /// <summary>Read a player from the table by stable ContentId, or null when not present.</summary>
    public static VisiblePlayer? GetPlayerByContentId(this IObjectTable objectTable, ulong contentId)
        => objectTable.FindPlayerCharacter(contentId) is { } pc ? ToVisiblePlayer(pc) : null;

    /// <summary>Locate the <b>live</b> game object for a ContentId, or null when
    /// the player isn't in the table (out of range, different zone, logged out).
    /// <para>Prefer <see cref="GetPlayerByContentId"/> — it hands back an immutable
    /// snapshot that is safe to keep. Use this overload only for state that must be
    /// read fresh and is never stored, such as <c>Position</c>, and consume the
    /// result within the same framework-thread callback.</para></summary>
    public static IPlayerCharacter? FindPlayerCharacter(this IObjectTable objectTable, ulong contentId)
    {
        foreach (var obj in objectTable)
        {
            if (obj is not IPlayerCharacter pc) continue;
            if (GetContentId(pc) != contentId) continue;
            return pc;
        }
        return null;
    }

    /// <summary>FFXIVClientStructs pointer hop to fetch ContentId — the public
    /// <see cref="IPlayerCharacter"/> surface doesn't expose it.</summary>
    public static unsafe ulong GetContentId(this IPlayerCharacter pc)
        => ((NativeChar*)pc.Address)->ContentId;

    private static VisiblePlayer ToVisiblePlayer(IPlayerCharacter pc) => new(
        ContentId: GetContentId(pc),
        GameObjectId: pc.GameObjectId,
        EntityId: pc.EntityId,
        Name: pc.Name.TextValue,
        HomeWorldId: pc.HomeWorld.RowId,
        CurrentWorldId: pc.CurrentWorld.RowId,
        ClassJobId: pc.ClassJob.RowId,
        Level: pc.Level,
        Customize: pc.Customize.ToArray(),
        CompanyTag: pc.CompanyTag.TextValue,
        // CurrentMount/CurrentMinion are Nullable<RowRef<T>> — null when nothing summoned.
        CurrentMountId: pc.CurrentMount?.RowId ?? 0,
        CurrentMinionId: pc.CurrentMinion?.RowId ?? 0,
        OnlineStatusId: pc.OnlineStatus.RowId);

    private static bool IsValid(VisiblePlayer p)
        => p.ContentId > 0
        && !string.IsNullOrEmpty(p.Name)
        && p.HomeWorldId != 0
        && p.HomeWorldId != ushort.MaxValue
        && p.ClassJobId != 0
        && p.EntityId != uint.MaxValue;
}