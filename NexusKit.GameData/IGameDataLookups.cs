namespace NexusKit.GameData;

/// <summary>
/// Opinionated convenience layer over <see cref="ISheetsProvider"/>. Wraps the lookups
/// that virtually every plugin needs (worlds, classjobs, territory, cosmetics) so
/// modules don't redo the same boilerplate around Lumina's SeString fields.
/// <para>All getters return <c>null</c> for unknown ids; callers shouldn't have to
/// distinguish "no row" from "empty name". Pass an explicit <see cref="GameDataClientLanguage"/>
/// when you want a name in a specific language; otherwise the provider's
/// <see cref="ISheetsProvider.CurrentLanguage"/> is used.</para>
/// </summary>
public interface IGameDataLookups
{
    // Worlds / Data centers
    string? GetWorldName(uint rowId);
    uint?   GetWorldIdByName(string name);
    string? GetDataCenterNameByWorldId(uint worldId);
    /// <summary>Locale-independent row-id variant of <see cref="GetDataCenterNameByWorldId"/>.
    /// Returns the world's <c>DataCenter.RowId</c>, or <c>null</c> when the
    /// world is unknown or has no DC assigned (e.g. test worlds).</summary>
    uint?   GetDataCenterIdByWorldId(uint worldId);
    string? GetDataCenterName(uint dataCenterRowId);
    IReadOnlyList<WorldInfo> GetWorldsInDataCenter(uint dataCenterId);

    // Territory / Zone
    string? GetTerritoryName(ushort territoryTypeId, GameDataClientLanguage? lang = null);

    /// <summary>
    /// Like <see cref="GetTerritoryName"/> but combines the parent zone
    /// (<c>TerritoryType.PlaceNameZone</c>) with the local place name when
    /// the local name alone would be ambiguous. E.g. Limsa Lominsa's upper
    /// decks have <c>PlaceName = "Obere Decks"</c> and
    /// <c>PlaceNameZone = "Limsa Lominsa"</c>; this method returns
    /// <c>"Limsa Lominsa - Obere Decks"</c>. Open-world zones whose place
    /// name already contains the parent zone (e.g. "Untere La Noscea" in
    /// zone "La Noscea") fall through to the bare place name to avoid
    /// duplication. Use this for display in lists where the user needs to
    /// distinguish similarly-named sub-areas across cities; use
    /// <see cref="GetTerritoryName"/> when the bare place name is required
    /// (e.g. Lifestream's residential aetheryte command parser).
    /// </summary>
    string? GetTerritoryDisplayName(ushort territoryTypeId, GameDataClientLanguage? lang = null);

    string? GetContentFinderConditionName(ushort territoryTypeId, GameDataClientLanguage? lang = null);

    /// <summary>
    /// The duty / instanced content name for a territory, or <c>null</c> when
    /// the territory is open-world / a city. Built on top of
    /// <see cref="GetContentFinderConditionName"/> with an extra filter that
    /// strips out CFC entries which only exist for engine plumbing
    /// (aetheryte / housing / roulette routing in cities — e.g. Limsa's
    /// "Untere Decks" CFC, whose name is just a substring of the territory's
    /// place name "Limsa Lominsa - Untere Decks"). Returns the CFC name only
    /// when it actually adds information over the place name, which is the
    /// case for every real Dungeon / Trial / Raid / PvP / Eureka / Bozja /
    /// Variant / Criterion content.
    /// </summary>
    string? GetInstancedContentName(ushort territoryTypeId, GameDataClientLanguage? lang = null);

    /// <summary>
    /// The localized category name of the duty associated with this
    /// territory (e.g. "Verließ" / "Dungeons", "Prüfung" / "Trials",
    /// "Schlachtzug" / "Raids", "PvP", …), resolved via
    /// <c>TerritoryType.ContentFinderCondition.ContentType.Name</c>.
    /// Returns <c>null</c> when the territory has no CFC, no ContentType
    /// link, or the ContentType's name is empty. Pairs with
    /// <see cref="GetInstancedContentName"/> for displaying typed
    /// "Prüfung: …" / "Raid: …" labels.
    /// </summary>
    string? GetContentTypeName(ushort territoryTypeId, GameDataClientLanguage? lang = null);

    /// <summary>
    /// Language-independent row id of the duty's <c>ContentType</c>, suitable
    /// as a key for UI palettes / icon maps. Same resolution path as
    /// <see cref="GetContentTypeName"/>; returns <c>null</c> when the
    /// territory has no CFC or no ContentType link.
    /// </summary>
    uint? GetContentTypeRowId(ushort territoryTypeId);

    // ClassJob
    string? GetClassJobName(uint rowId, GameDataClientLanguage? lang = null);
    string? GetClassJobAbbreviation(uint rowId);
    JobRole GetClassJobRole(uint rowId);

    // Cosmetic / inventory
    string? GetItemName(uint rowId, GameDataClientLanguage? lang = null);
    string? GetMountName(uint rowId, GameDataClientLanguage? lang = null);
    string? GetMinionName(uint rowId, GameDataClientLanguage? lang = null);
    string? GetTitleName(uint rowId, bool feminine, GameDataClientLanguage? lang = null);
    string? GetOrnamentName(uint rowId, GameDataClientLanguage? lang = null);

    // Race / Tribe
    string? GetRaceName(uint rowId, bool feminine, GameDataClientLanguage? lang = null);
    string? GetTribeName(uint rowId, bool feminine, GameDataClientLanguage? lang = null);

    // Online status (RolePlay / AFK / Busy / Mentor / …)
    string? GetOnlineStatusName(uint rowId, GameDataClientLanguage? lang = null);

    // Grand Company
    string? GetGrandCompanyName(uint rowId, GameDataClientLanguage? lang = null);

    /// <summary>
    /// Reproduce a localized GC rank name from the tuple stored by
    /// <see cref="IGameDataResolver.ResolveGrandCompanyRank"/>. Returns null when
    /// any of the three coordinates points outside the sheet ranges.
    /// </summary>
    string? GetGrandCompanyRankName(byte grandCompanyId, byte rankId, bool isFeminine, GameDataClientLanguage? lang = null);
}
