using System.Text.RegularExpressions;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace NexusKit.GameData;

internal sealed class GameDataResolver : IGameDataResolver
{
    private readonly ISheetsProvider mSheets;
    private readonly object mCacheLock = new();
    private readonly Dictionary<(GameDataKind, GameDataClientLanguage), IReadOnlyDictionary<string, uint>> mCache = new();
    private readonly Dictionary<(GameDataKind, GameDataClientLanguage), IReadOnlyDictionary<string, uint>> mNormalizedCache = new();
    private readonly object mGcRankLock = new();
    private IReadOnlyDictionary<(GameDataClientLanguage, string), GrandCompanyRankRef>? mGcRankCache;

    public GameDataResolver(ISheetsProvider sheets)
    {
        mSheets = sheets;
    }

    public uint? ResolveIdByName(string name, GameDataKind kind, GameDataClientLanguage language)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var trimmed = name.Trim();
        var map = GetMap(kind, language);
        if (map.TryGetValue(trimmed, out var rowId)) return rowId;

        // GrandCompany rows on the Lodestone character page emit a shortened variant
        // ("Morgenviper" instead of the full "Bruderschaft der Morgenviper" that the
        // FC page uses). Fall back to substring matching — there are only three real
        // rows so collision risk is nil and the short forms are unambiguous.
        if (kind == GameDataKind.GrandCompany)
        {
            foreach (var (key, value) in map)
            {
                if (value == 0) continue;
                if (key.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                    || trimmed.Contains(key, StringComparison.OrdinalIgnoreCase))
                    return value;
            }
        }

        return null;
    }

    public GrandCompanyRankRef? ResolveGrandCompanyRank(string rankName, GameDataClientLanguage language)
    {
        if (string.IsNullOrWhiteSpace(rankName)) return null;
        var stripped = StripMacros(rankName);
        var normalized = Normalize(stripped);
        if (normalized.Length == 0) return null;

        var map = GetOrBuildGcRankMap();
        return map.TryGetValue((language, normalized), out var r) ? r : null;
    }

    private IReadOnlyDictionary<(GameDataClientLanguage, string), GrandCompanyRankRef> GetOrBuildGcRankMap()
    {
        if (mGcRankCache is not null) return mGcRankCache;
        lock (mGcRankLock)
        {
            if (mGcRankCache is not null) return mGcRankCache;

            var map = new Dictionary<(GameDataClientLanguage, string), GrandCompanyRankRef>();
            foreach (var lang in Enum.GetValues<GameDataClientLanguage>())
            {
                AddGcRankSheet<GCRankLimsaMaleText>(   map, lang, gcId: 1, feminine: false, r => r.Singular);
                AddGcRankSheet<GCRankLimsaFemaleText>( map, lang, gcId: 1, feminine: true,  r => r.Singular);
                AddGcRankSheet<GCRankGridaniaMaleText>(  map, lang, gcId: 2, feminine: false, r => r.Singular);
                AddGcRankSheet<GCRankGridaniaFemaleText>(map, lang, gcId: 2, feminine: true,  r => r.Singular);
                AddGcRankSheet<GCRankUldahMaleText>(   map, lang, gcId: 3, feminine: false, r => r.Singular);
                AddGcRankSheet<GCRankUldahFemaleText>( map, lang, gcId: 3, feminine: true,  r => r.Singular);
            }
            mGcRankCache = map;
            return mGcRankCache;
        }
    }

    private void AddGcRankSheet<T>(
        Dictionary<(GameDataClientLanguage, string), GrandCompanyRankRef> sink,
        GameDataClientLanguage lang, byte gcId, bool feminine,
        Func<T, ReadOnlySeString> singularSelector)
        where T : struct, IExcelRow<T>
    {
        var sheet = mSheets.GetSheet<T>(lang);
        if (sheet is null) return;
        foreach (var row in sheet)
        {
            // Row 0 is the empty/sentinel row in every GC rank sheet — skip it so an
            // empty scraped rank name doesn't collide with a real entry.
            if (row.RowId == 0) continue;
            var singular = StripMacros(singularSelector(row).ExtractText());
            if (singular.Length == 0) continue;
            var n = Normalize(singular);
            if (n.Length == 0) continue;
            var key = (lang, n);
            // First-write-wins: male and female sheets share many names ("Auge der Schlange")
            // but the resolver caller already commits to a specific gender via the result, so
            // a collision here just means we picked the first-registered sheet variant.
            // For our user-facing lookup that's fine — the rank ID + GC are still correct,
            // only the feminine flag may differ for the genuinely shared rows.
            if (!sink.ContainsKey(key))
                sink[key] = new GrandCompanyRankRef((byte)row.RowId, gcId, feminine);
        }
    }

    public uint? ResolveIdByNormalizedName(string name, GameDataKind kind, GameDataClientLanguage language)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var normalized = Normalize(name);
        if (normalized.Length == 0) return null;

        var map = GetNormalizedMap(kind, language);
        return map.TryGetValue(normalized, out var rowId) ? rowId : null;
    }

    private IReadOnlyDictionary<string, uint> GetNormalizedMap(GameDataKind kind, GameDataClientLanguage language)
    {
        var key = (kind, language);
        lock (mCacheLock)
        {
            if (mNormalizedCache.TryGetValue(key, out var existing)) return existing;

            // Derive the normalized map from the regular one to avoid scanning the sheet twice.
            var source = GetMapLocked(kind, language);
            var dict = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in source)
            {
                var n = Normalize(kvp.Key);
                if (n.Length == 0) continue;
                // Last-write-wins matches the regular map's policy.
                dict[n] = kvp.Value;
            }
            mNormalizedCache[key] = dict;
            return dict;
        }
    }

    private IReadOnlyDictionary<string, uint> GetMapLocked(GameDataKind kind, GameDataClientLanguage language)
    {
        var key = (kind, language);
        if (mCache.TryGetValue(key, out var existing)) return existing;
        var built = Build(kind, language);
        mCache[key] = built;
        return built;
    }

    private static string Normalize(string s)
    {
        var buf = new char[s.Length];
        var len = 0;
        foreach (var c in s)
            if (char.IsLetterOrDigit(c)) buf[len++] = char.ToLowerInvariant(c);
        return new string(buf, 0, len);
    }

    private IReadOnlyDictionary<string, uint> GetMap(GameDataKind kind, GameDataClientLanguage language)
    {
        lock (mCacheLock)
        {
            return GetMapLocked(kind, language);
        }
    }

    private IReadOnlyDictionary<string, uint> Build(GameDataKind kind, GameDataClientLanguage language) => kind switch
    {
        GameDataKind.Mount           => BuildMap<Mount>(language,        r => r.Singular),
        GameDataKind.Minion          => BuildMap<Companion>(language,    r => r.Singular),
        GameDataKind.Item            => BuildMap<Item>(language,         r => r.Name),
        GameDataKind.World           => BuildWorldMap(),
        GameDataKind.ClassJob        => BuildMap<ClassJob>(language,     r => r.Name),
        GameDataKind.Ornament        => BuildMap<Ornament>(language,     r => r.Singular),
        GameDataKind.GrandCompany    => BuildMap<GrandCompany>(language, r => r.Name),
        GameDataKind.DataCenter      => BuildMap<WorldDCGroupType>(language, r => r.Name),
        GameDataKind.HousingDistrict => BuildHousingDistrictMap(language),
        _                            => EmptyMap,
    };

    private IReadOnlyDictionary<string, uint> BuildHousingDistrictMap(GameDataClientLanguage language)
    {
        // TerritoryType has ~3000+ rows — iterating it just to find 5 is wasteful.
        // GetRowOrDefault each known id from ResidentialDistricts, read its
        // localized PlaceName, register so a localized scrape like
        // "Dorf des Nebels" resolves to the same 339 the EN scrape "Mist" produces.
        var sheet = mSheets.GetSheet<TerritoryType>(language);
        if (sheet is null) return EmptyMap;

        var dict = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ResidentialDistricts.AllTerritoryIds)
        {
            if (sheet.GetRowOrDefault(id) is not { } row) continue;
            var name = StripMacros(row.PlaceName.Value.Name.ExtractText());
            if (string.IsNullOrWhiteSpace(name)) continue;
            dict[name] = id;
        }
        return dict;
    }

    private IReadOnlyDictionary<string, uint> BuildMap<T>(
        GameDataClientLanguage language,
        Func<T, ReadOnlySeString> nameSelector)
        where T : struct, IExcelRow<T>
    {
        var sheet = mSheets.GetSheet<T>(language);
        if (sheet is null) return EmptyMap;

        // Last-row-wins is fine for our purposes (a few rows share names with rowId 0
        // sentinels that we'd want overridden by the real entry anyway).
        var dict = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in sheet)
        {
            var name = StripMacros(nameSelector(row).ExtractText());
            if (string.IsNullOrWhiteSpace(name)) continue;
            dict[name] = row.RowId;
        }
        return dict;
    }

    private IReadOnlyDictionary<string, uint> BuildWorldMap()
    {
        // World is language-invariant (server names aren't localized), and we want to
        // skip private/test worlds the same way GetWorldIdByName does.
        var sheet = mSheets.GetSheet<World>();
        if (sheet is null) return EmptyMap;

        var dict = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in sheet)
        {
            if (!row.IsPublic) continue;
            var name = StripMacros(row.Name.ExtractText());
            if (string.IsNullOrWhiteSpace(name)) continue;
            dict[name] = row.RowId;
        }
        return dict;
    }

    /// <summary>
    /// <c>Lumina.ExtractText()</c> on German (and some other languages) leaves macro
    /// placeholders like <c>[p]</c>, <c>[t]</c> inside the string — these encode
    /// plural/article/gender substitution and aren't rendered in the player-facing UI.
    /// Strip them so cached keys line up with the names third-party APIs scrape from
    /// the live game / Lodestone HTML.
    /// </summary>
    private static string StripMacros(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        // Match [a-z…]-only single-token brackets; preserve real bracketed content
        // (e.g., subtitle "[Light]") if it ever appears. Then collapse whitespace.
        var stripped = Regex.Replace(s, @"\[[a-zA-Z]+\]", string.Empty);
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    private static readonly IReadOnlyDictionary<string, uint> EmptyMap
        = new Dictionary<string, uint>(0);
}
