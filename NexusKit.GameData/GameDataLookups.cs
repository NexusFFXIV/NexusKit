using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace NexusKit.GameData;

internal sealed class GameDataLookups : IGameDataLookups
{
    private readonly ISheetsProvider mSheets;

    // Name-by-row-id results are hammered every frame by the UI (player list,
    // detail panels, debug surfaces). Each resolution does a sheet lookup plus
    // two regex passes inside Sanitize — fine once, expensive at frame rate.
    // Cache by (family, language, rowId, variant) so repeated hits in the same
    // session are a single dictionary lookup. Null results are cached too —
    // missing rows shouldn't trigger a re-lookup on every render.
    private readonly ConcurrentDictionary<NameKey, string?> mNameCache = new();

    private readonly record struct NameKey(byte Family, byte Lang, byte Variant, uint RowId);

    private enum NameFamily : byte
    {
        World, DataCenterByWorld, DataCenter,
        Territory, TerritoryDisplay, ContentFinderCondition, ContentType,
        ClassJobName, ClassJobAbbr,
        Item, Mount, Minion, Title, Ornament,
        Race, Tribe,
        GrandCompany, GrandCompanyRank,
        OnlineStatus,
    }

    public GameDataLookups(ISheetsProvider sheets)
    {
        mSheets = sheets;
    }

    private string? GetCached(NameFamily family, GameDataClientLanguage? lang, uint rowId,
                              byte variant, Func<string?> build)
    {
        // Null lang means "current language" — use a sentinel byte so the entry
        // pairs up with whatever the sheets provider currently resolves to. On a
        // runtime language switch consumers can call Clear() to drop stale entries.
        var langKey = (byte)(lang is { } l ? (int)l + 1 : 0);
        var key = new NameKey((byte)family, langKey, variant, rowId);
        if (mNameCache.TryGetValue(key, out var cached)) return cached;
        var fresh = build();
        mNameCache[key] = fresh;
        return fresh;
    }

    /// <summary>Drop every cached name. Call after a runtime language change
    /// so subsequent reads pick up the new sheet translation.</summary>
    public void Clear()
    {
        mNameCache.Clear();
        mContentTypeRowIdCache.Clear();
    }

    // ---------- Worlds / Data centers ----------

    public string? GetWorldName(uint rowId)
        => GetCached(NameFamily.World, null, rowId, 0, () =>
            mSheets.GetSheet<World>()?.GetRowOrDefault(rowId) is { } row && row.IsPublic
                ? Sanitize(row.Name) : null);

    public uint? GetWorldIdByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var sheet = mSheets.GetSheet<World>();
        if (sheet is null) return null;
        foreach (var w in sheet)
            if (w.IsPublic && StringEquals(w.Name, name))
                return w.RowId;
        return null;
    }

    public string? GetDataCenterNameByWorldId(uint worldId)
        => GetCached(NameFamily.DataCenterByWorld, null, worldId, 0, () =>
        {
            var sheet = mSheets.GetSheet<World>();
            if (sheet?.GetRowOrDefault(worldId) is not { } w) return null;
            return Sanitize(w.DataCenter.Value.Name);
        });

    public uint? GetDataCenterIdByWorldId(uint worldId)
    {
        var sheet = mSheets.GetSheet<World>();
        if (sheet?.GetRowOrDefault(worldId) is not { } w) return null;
        return w.DataCenter.RowId == 0 ? null : w.DataCenter.RowId;
    }

    public string? GetDataCenterName(uint dataCenterRowId)
        => GetCached(NameFamily.DataCenter, null, dataCenterRowId, 0, () =>
        {
            var sheet = mSheets.GetSheet<WorldDCGroupType>();
            return sheet?.GetRowOrDefault(dataCenterRowId) is { } row ? Sanitize(row.Name) : null;
        });

    public IReadOnlyList<WorldInfo> GetWorldsInDataCenter(uint dataCenterId)
    {
        var sheet = mSheets.GetSheet<World>();
        if (sheet is null) return Array.Empty<WorldInfo>();

        var result = new List<WorldInfo>();
        foreach (var w in sheet)
        {
            if (!w.IsPublic) continue;
            if (w.DataCenter.RowId != dataCenterId) continue;
            result.Add(new WorldInfo(w.RowId, Sanitize(w.Name) ?? string.Empty, w.DataCenter.RowId));
        }
        return result;
    }

    // ---------- Territory / Zone ----------

    public string? GetTerritoryName(ushort territoryTypeId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.Territory, lang, territoryTypeId, 0, () =>
        {
            // TerritoryType itself has no language-specific columns, so passing
            // `lang` to GetSheet<TerritoryType> is a no-op — the localized text
            // lives in PlaceName. Following `row.PlaceName.Value` resolves via
            // the Lumina module's default language (the user's UI language),
            // which silently returns e.g. "Kelchkuppe" on a DE client even when
            // English was requested. Explicitly fetch the PlaceName sheet in
            // the requested language and look up by RowId so callers that ask
            // for English (e.g. the Lifestream adapter building a /li command)
            // actually get English.
            var territorySheet = mSheets.GetSheet<TerritoryType>();
            if (territorySheet?.GetRowOrDefault(territoryTypeId) is not { } row) return null;
            var placeNameId = row.PlaceName.RowId;
            if (placeNameId == 0) return null;
            var placeNameSheet = mSheets.GetSheet<PlaceName>(lang);
            if (placeNameSheet?.GetRowOrDefault(placeNameId) is not { } placeRow) return null;
            return Sanitize(placeRow.Name);
        });

    public string? GetTerritoryDisplayName(ushort territoryTypeId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.TerritoryDisplay, lang, territoryTypeId, 0, () =>
        {
            // Same per-language PlaceName resolution as GetTerritoryName — see
            // its comment for why we go via the languaged PlaceName sheet
            // instead of following row.PlaceName.Value.
            var territorySheet = mSheets.GetSheet<TerritoryType>();
            if (territorySheet?.GetRowOrDefault(territoryTypeId) is not { } row) return null;
            var placeNameId = row.PlaceName.RowId;
            if (placeNameId == 0) return null;
            var placeNameSheet = mSheets.GetSheet<PlaceName>(lang);
            if (placeNameSheet?.GetRowOrDefault(placeNameId) is not { } placeRow) return null;
            var placeName = Sanitize(placeRow.Name);
            if (placeName is null) return null;

            // City districts store only the local part in PlaceName ("Obere
            // Decks") and the parent city ("Limsa Lominsa") in PlaceNameZone.
            // Combine the two so the display reads "Limsa Lominsa - Obere
            // Decks". For open-world zones where the place name already
            // contains the parent zone name ("Untere La Noscea" inside zone
            // "La Noscea"), or where there is no parent (Mor Dhona, instanced
            // duty maps), we fall through to the bare place name.
            var zoneNameId = row.PlaceNameZone.RowId;
            if (zoneNameId == 0 || zoneNameId == placeNameId) return placeName;
            if (placeNameSheet.GetRowOrDefault(zoneNameId) is not { } zoneRow) return placeName;
            var zoneName = Sanitize(zoneRow.Name);
            if (string.IsNullOrEmpty(zoneName)) return placeName;
            if (placeName.Contains(zoneName, StringComparison.OrdinalIgnoreCase)) return placeName;
            return $"{zoneName} - {placeName}";
        });

    public string? GetContentFinderConditionName(ushort territoryTypeId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.ContentFinderCondition, lang, territoryTypeId, 0, () =>
        {
            // ContentFinderCondition is language-aware, so we can ask the
            // languaged sheet directly via the TerritoryType row's link id.
            var territorySheet = mSheets.GetSheet<TerritoryType>();
            if (territorySheet?.GetRowOrDefault(territoryTypeId) is not { } row) return null;
            var cfcId = row.ContentFinderCondition.RowId;
            if (cfcId == 0) return null;
            var cfcSheet = mSheets.GetSheet<ContentFinderCondition>(lang);
            if (cfcSheet?.GetRowOrDefault(cfcId) is not { } cfcRow) return null;
            return Sanitize(cfcRow.Name);
        });

    public string? GetContentTypeName(ushort territoryTypeId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.ContentType, lang, territoryTypeId, 0, () =>
        {
            // Walk TerritoryType -> ContentFinderCondition -> ContentType. The
            // ContentType sheet is language-aware so we request the languaged
            // sheet directly. Rows like "Dungeons", "Trials", "Raids",
            // "PvP", "Disciples of the Land", "Treasure Hunt" — what we want
            // are the combat-instance entries; gathering/treasure tables also
            // exist but in practice they don't reach the encounters tab
            // (no non-local sightings), so no filter is needed here.
            var territorySheet = mSheets.GetSheet<TerritoryType>();
            if (territorySheet?.GetRowOrDefault(territoryTypeId) is not { } row) return null;
            var cfcId = row.ContentFinderCondition.RowId;
            if (cfcId == 0) return null;
            var cfcSheet = mSheets.GetSheet<ContentFinderCondition>();
            if (cfcSheet?.GetRowOrDefault(cfcId) is not { } cfcRow) return null;
            var contentTypeId = cfcRow.ContentType.RowId;
            if (contentTypeId == 0) return null;
            var ctSheet = mSheets.GetSheet<ContentType>(lang);
            if (ctSheet?.GetRowOrDefault(contentTypeId) is not { } ctRow) return null;
            return Sanitize(ctRow.Name);
        });

    // ContentType.RowId is language-independent — a small dedicated cache
    // keeps the per-frame lookup cost flat without polluting mNameCache's
    // language-keyed entries with a value that doesn't vary by language.
    private readonly ConcurrentDictionary<ushort, uint?> mContentTypeRowIdCache = new();

    public uint? GetContentTypeRowId(ushort territoryTypeId)
    {
        if (mContentTypeRowIdCache.TryGetValue(territoryTypeId, out var cached)) return cached;
        uint? result = null;
        var territorySheet = mSheets.GetSheet<TerritoryType>();
        if (territorySheet?.GetRowOrDefault(territoryTypeId) is { } row)
        {
            var cfcId = row.ContentFinderCondition.RowId;
            if (cfcId != 0
                && mSheets.GetSheet<ContentFinderCondition>()?.GetRowOrDefault(cfcId) is { } cfcRow
                && cfcRow.ContentType.RowId != 0)
            {
                result = cfcRow.ContentType.RowId;
            }
        }
        mContentTypeRowIdCache[territoryTypeId] = result;
        return result;
    }

    public string? GetInstancedContentName(ushort territoryTypeId, GameDataClientLanguage? lang = null)
    {
        // Not cached at this level — the two delegated lookups are themselves
        // cached, and the substring comparison is cheap. Caching here would
        // duplicate strings for every (territory, language) tuple.
        var cfcName = GetContentFinderConditionName(territoryTypeId, lang);
        if (string.IsNullOrEmpty(cfcName)) return null;
        var placeName = GetTerritoryName(territoryTypeId, lang);
        if (!string.IsNullOrEmpty(placeName)
            && cfcName.Length < placeName.Length
            && placeName.Contains(cfcName, StringComparison.OrdinalIgnoreCase))
        {
            // The CFC name is a proper shorter substring of the place name —
            // typically a city sub-district (CFC "Untere Decks" inside place
            // "Limsa Lominsa - Untere Decks") that exists only because the
            // engine wires aetheryte / housing routing through CFC. Not a
            // real instance — the place name is more informative.
            return null;
        }
        return cfcName;
    }

    // ---------- ClassJob ----------

    public string? GetClassJobName(uint rowId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.ClassJobName, lang, rowId, 0, () =>
            mSheets.GetSheet<ClassJob>(lang)?.GetRowOrDefault(rowId) is { } row
                ? Sanitize(row.Name) : null);

    public string? GetClassJobAbbreviation(uint rowId)
        => GetCached(NameFamily.ClassJobAbbr, null, rowId, 0, () =>
            mSheets.GetSheet<ClassJob>()?.GetRowOrDefault(rowId) is { } row
                ? Sanitize(row.Abbreviation) : null);

    public JobRole GetClassJobRole(uint rowId)
    {
        var sheet = mSheets.GetSheet<ClassJob>();
        if (sheet?.GetRowOrDefault(rowId) is not { } row) return JobRole.Unknown;

        // DoH (Crafter) / DoL (Gatherer) — ClassJob.Role is 0 for these, JobType differentiates.
        if (row.DohDolJobIndex >= 0 && row.DohDolJobIndex <= 7) return JobRole.Crafter;
        if (row.DohDolJobIndex >= 8) return JobRole.Gatherer;
        if (row.ClassJobCategory.RowId == 0) return JobRole.Unknown;

        return row.Role switch
        {
            1 => JobRole.Tank,
            2 => JobRole.MeleeDps,
            3 => JobRole.RangedDps,
            4 => JobRole.Healer,
            _ => JobRole.Unknown,
        };
    }

    // ---------- Cosmetic / inventory ----------

    public string? GetItemName(uint rowId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.Item, lang, rowId, 0, () =>
            mSheets.GetSheet<Item>(lang)?.GetRowOrDefault(rowId) is { } row
                ? Sanitize(row.Name) : null);

    public string? GetMountName(uint rowId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.Mount, lang, rowId, 0, () =>
            mSheets.GetSheet<Mount>(lang)?.GetRowOrDefault(rowId) is { } row
                ? Sanitize(row.Singular) : null);

    public string? GetMinionName(uint rowId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.Minion, lang, rowId, 0, () =>
            mSheets.GetSheet<Companion>(lang)?.GetRowOrDefault(rowId) is { } row
                ? Sanitize(row.Singular) : null);

    public string? GetTitleName(uint rowId, bool feminine, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.Title, lang, rowId, feminine ? (byte)1 : (byte)0, () =>
        {
            var sheet = mSheets.GetSheet<Title>(lang);
            if (sheet?.GetRowOrDefault(rowId) is not { } row) return null;
            return Sanitize(feminine ? row.Feminine : row.Masculine);
        });

    public string? GetOrnamentName(uint rowId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.Ornament, lang, rowId, 0, () =>
            mSheets.GetSheet<Ornament>(lang)?.GetRowOrDefault(rowId) is { } row
                ? Sanitize(row.Singular) : null);

    // ---------- Race / Tribe ----------

    public string? GetRaceName(uint rowId, bool feminine, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.Race, lang, rowId, feminine ? (byte)1 : (byte)0, () =>
        {
            var sheet = mSheets.GetSheet<Race>(lang);
            if (sheet?.GetRowOrDefault(rowId) is not { } row) return null;
            return Sanitize(feminine ? row.Feminine : row.Masculine);
        });

    public string? GetTribeName(uint rowId, bool feminine, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.Tribe, lang, rowId, feminine ? (byte)1 : (byte)0, () =>
        {
            var sheet = mSheets.GetSheet<Tribe>(lang);
            if (sheet?.GetRowOrDefault(rowId) is not { } row) return null;
            return Sanitize(feminine ? row.Feminine : row.Masculine);
        });

    // ---------- Online status ----------

    public string? GetOnlineStatusName(uint rowId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.OnlineStatus, lang, rowId, 0, () =>
            mSheets.GetSheet<OnlineStatus>(lang)?.GetRowOrDefault(rowId) is { } row
                ? Sanitize(row.Name) : null);

    // ---------- Grand Company ----------

    public string? GetGrandCompanyName(uint rowId, GameDataClientLanguage? lang = null)
        => GetCached(NameFamily.GrandCompany, lang, rowId, 0, () =>
            mSheets.GetSheet<GrandCompany>(lang)?.GetRowOrDefault(rowId) is { } row
                ? Sanitize(row.Name) : null);

    public string? GetGrandCompanyRankName(byte grandCompanyId, byte rankId, bool isFeminine, GameDataClientLanguage? lang = null)
    {
        // Pack (grandCompanyId 1..3 + feminine flag) into a single 0..5 variant
        // so each (gc, gender) pair gets its own cache slot keyed by rankId.
        var variant = (byte)((grandCompanyId - 1) * 2 + (isFeminine ? 1 : 0));
        return GetCached(NameFamily.GrandCompanyRank, lang, rankId, variant, () =>
            (grandCompanyId, isFeminine) switch
            {
                (1, false) => LookupRank<GCRankLimsaMaleText>(rankId, lang, r => r.Singular),
                (1, true)  => LookupRank<GCRankLimsaFemaleText>(rankId, lang, r => r.Singular),
                (2, false) => LookupRank<GCRankGridaniaMaleText>(rankId, lang, r => r.Singular),
                (2, true)  => LookupRank<GCRankGridaniaFemaleText>(rankId, lang, r => r.Singular),
                (3, false) => LookupRank<GCRankUldahMaleText>(rankId, lang, r => r.Singular),
                (3, true)  => LookupRank<GCRankUldahFemaleText>(rankId, lang, r => r.Singular),
                _ => null,
            });
    }

    private string? LookupRank<T>(byte rowId, GameDataClientLanguage? lang, Func<T, ReadOnlySeString> selector)
        where T : struct, Lumina.Excel.IExcelRow<T>
    {
        var sheet = mSheets.GetSheet<T>(lang);
        if (sheet?.GetRowOrDefault(rowId) is not { } row) return null;
        return Sanitize(selector(row));
    }

    // ---------- helpers ----------

    // Compile once at type init — Regex.Replace with a string pattern rebuilds the
    // engine on every call, which adds up when Sanitize runs at frame rate.
    private static readonly Regex MacroRegex = new(@"\[[a-zA-Z]+\]", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static string? Sanitize(ReadOnlySeString s)
    {
        var t = s.ExtractText();
        if (string.IsNullOrEmpty(t)) return null;
        // Lumina's German (and some other) ExtractText leaves macro placeholders such
        // as [p]/[t] in the string — these aren't part of the player-facing rendering
        // and would otherwise leak into UI / persistence.
        var stripped = MacroRegex.Replace(t, string.Empty);
        stripped = WhitespaceRegex.Replace(stripped, " ").Trim();
        return stripped.Length == 0 ? null : stripped;
    }

    private static bool StringEquals(ReadOnlySeString s, string other)
        => string.Equals(Sanitize(s), other, StringComparison.OrdinalIgnoreCase);
}
