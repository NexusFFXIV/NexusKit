namespace NexusKit.GameData;

/// <summary>
/// Single source of truth for the five FFXIV residential housing districts.
/// Stable <c>TerritoryType.RowId</c> values plus convenience accessors.
/// <para>Used by <see cref="GameDataResolver"/> to build the name→id map and by
/// foreign-plugin adapters (e.g. Lifestream) to map between <c>EstateAddress</c>
/// and the targets their commands understand.</para>
/// </summary>
public static class ResidentialDistricts
{
    public const uint MistTerritoryId = 339;          // Limsa Lominsa housing
    public const uint LavenderBedsTerritoryId = 340;  // Gridania housing
    public const uint GobletTerritoryId = 341;        // Ul'dah housing
    public const uint ShiroganeTerritoryId = 641;     // Kugane housing
    public const uint EmpyreumTerritoryId = 979;      // Ishgard housing

    /// <summary>The five stable district row ids, in introduction order.</summary>
    public static IReadOnlyList<uint> AllTerritoryIds { get; } = new[]
    {
        MistTerritoryId,
        LavenderBedsTerritoryId,
        GobletTerritoryId,
        ShiroganeTerritoryId,
        EmpyreumTerritoryId,
    };

    /// <summary>True when <paramref name="territoryId"/> is one of the five
    /// residential districts.</summary>
    public static bool IsResidential(uint territoryId)
    {
        foreach (var id in AllTerritoryIds)
            if (id == territoryId) return true;
        return false;
    }
}
