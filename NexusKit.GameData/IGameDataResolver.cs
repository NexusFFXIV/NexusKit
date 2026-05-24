namespace NexusKit.GameData;

/// <summary>
/// Name → RowId lookup for the kinds in <see cref="GameDataKind"/>. Underpins the
/// "store IDs not strings" pattern for external modules: scrape a localized name
/// from a third-party API, resolve to a Lumina RowId once, persist the ID — and
/// reload the localized display name later through <see cref="IGameDataLookups"/>.
/// <para>Per-(kind, language) caches are built lazily on first request and cached
/// for the rest of the session. Lookups are case-insensitive on the trimmed name.</para>
/// </summary>
public interface IGameDataResolver
{
    /// <summary>
    /// Resolve a localized name to a Lumina RowId. Returns null when no row matches.
    /// </summary>
    /// <param name="name">Display name as it appears on the third-party source (Lodestone,
    /// FFXIVCollect, etc.) in the given <paramref name="language"/>.</param>
    /// <param name="kind">Which sheet to consult.</param>
    /// <param name="language">Language the supplied <paramref name="name"/> is in.</param>
    uint? ResolveIdByName(string name, GameDataKind kind, GameDataClientLanguage language);

    /// <summary>
    /// Variant of <see cref="ResolveIdByName"/> for sources whose spacing/punctuation
    /// doesn't match Lumina's. Strips every non-alphanumeric character from both the
    /// query and the cached sheet names before comparing, so e.g. NetStone's
    /// PascalCase class-job names (<c>BlueMage</c>, <c>DarkKnight</c>) match
    /// Lumina's spaced English names (<c>Blue Mage</c>, <c>Dark Knight</c>).
    /// </summary>
    uint? ResolveIdByNormalizedName(string name, GameDataKind kind, GameDataClientLanguage language);

    /// <summary>
    /// Resolve a Lodestone-scraped Grand Company rank name (e.g. <c>"Erleuchtete
    /// Schlangenpriesterin"</c>) to the tuple needed to reproduce it later
    /// (Lumina rank-row id, Grand Company id, feminine-or-masculine variant).
    /// Returns null if no matching row is found in any of the six per-GC/gender sheets.
    /// </summary>
    GrandCompanyRankRef? ResolveGrandCompanyRank(string rankName, GameDataClientLanguage language);
}
