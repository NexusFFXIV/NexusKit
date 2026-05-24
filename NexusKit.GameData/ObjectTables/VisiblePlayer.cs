namespace NexusKit.GameData.ObjectTables;

/// <summary>
/// Lightweight, immutable snapshot of a player character read from the live
/// Dalamud <c>IObjectTable</c> in a single framework-thread tick. Captures the
/// fields a tracker / observation pipeline actually consumes; the unsafe
/// FFXIVClientStructs pulls that aren't worth their complexity here (Title /
/// Ornament) are deliberately omitted — add later when needed.
/// <para>The snapshot is detached from the live object so it's safe to forward
/// to threads other than the framework thread.</para>
/// </summary>
/// <param name="ContentId">Stable cross-session 64-bit identifier. Read via an
/// unsafe FFXIVClientStructs hop because the public <c>IPlayerCharacter</c>
/// interface doesn't expose it. Always &gt; 0 for valid characters.</param>
/// <param name="GameObjectId">Address-style id; survives only while the object
/// is allocated in the table. Useful for "is this the same object" within a frame.</param>
/// <param name="EntityId">Network entity id; stable within an instance.</param>
/// <param name="Name">Character first + last name as the in-game string.</param>
/// <param name="HomeWorldId">Lumina <c>World.RowId</c> of the character's home world.</param>
/// <param name="CurrentWorldId">Live world (differs from <see cref="HomeWorldId"/>
/// when world-visiting).</param>
/// <param name="ClassJobId">Lumina <c>ClassJob.RowId</c> of the currently-equipped job.</param>
/// <param name="Level">Level on the current job.</param>
/// <param name="Customize">26+ byte appearance array (race, tribe, gender, hair, face,
/// scars, eyes, lips, etc.). Detached copy — never the underlying memory.</param>
/// <param name="CompanyTag">Free-company tag the character is wearing, or empty
/// when not in an FC.</param>
/// <param name="CurrentMountId">Mount row currently summoned (0 = none).</param>
/// <param name="CurrentMinionId">Minion row currently summoned (0 = none).</param>
/// <param name="OnlineStatusId">Lumina <c>OnlineStatus.RowId</c> (RolePlay, AFK,
/// CommerceMode, etc.) — 0 = no status.</param>
public sealed record VisiblePlayer(
    ulong ContentId,
    ulong GameObjectId,
    uint EntityId,
    string Name,
    uint HomeWorldId,
    uint CurrentWorldId,
    uint ClassJobId,
    byte Level,
    byte[] Customize,
    string CompanyTag,
    uint CurrentMountId,
    uint CurrentMinionId,
    uint OnlineStatusId);
