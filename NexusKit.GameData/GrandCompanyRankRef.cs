namespace NexusKit.GameData;

/// <summary>
/// Result of resolving a Lodestone-scraped Grand Company rank name. The full
/// rank identity is a tuple of (Grand Company × gender × numeric rank tier);
/// stored together because the rank tier alone is meaningless without the
/// gender and GC needed to reproduce the localized name.
/// </summary>
/// <param name="RankId">Lumina RowId of the resolved rank row (0-19 in current data).</param>
/// <param name="GrandCompanyId">Lumina <c>GrandCompany.RowId</c> (1 = Maelstrom, 2 = Twin Adder, 3 = Immortal Flames).</param>
/// <param name="IsFeminine">True when the rank text comes from the feminine sheet variant.
/// Determines which sheet to consult when re-rendering the localized name later.</param>
public sealed record GrandCompanyRankRef(byte RankId, byte GrandCompanyId, bool IsFeminine);
