using System.Globalization;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using NexusKit.Core.Localization;

namespace NexusKit.GameData;

internal sealed class SheetsProvider : ISheetsProvider
{
    private readonly IDataManager mDataManager;
    private readonly LocalizationManager? mLocalization;
    private readonly object mCacheLock = new();
    private readonly Dictionary<(Type, GameDataClientLanguage), object?> mCache = new();

    public SheetsProvider(IDataManager dataManager, LocalizationManager? localization = null)
    {
        mDataManager = dataManager;
        mLocalization = localization;
    }

    public GameDataClientLanguage CurrentLanguage
    {
        get
        {
            // LocalizationManager (when present) is the canonical source — it already
            // bridges Dalamud's UI language and explicit user/plugin overrides.
            if (mLocalization is not null && TryFromCulture(mLocalization.CurrentCulture, out var lang))
                return lang;
            return FromDalamud(mDataManager.Language);
        }
    }

    public ExcelSheet<T>? GetSheet<T>(GameDataClientLanguage? language = null)
        where T : struct, IExcelRow<T>
    {
        var lang = language ?? CurrentLanguage;
        var key = (typeof(T), lang);

        lock (mCacheLock)
        {
            if (mCache.TryGetValue(key, out var cached))
                return (ExcelSheet<T>?)cached;

            var sheet = mDataManager.GetExcelSheet<T>(ToDalamud(lang));
            mCache[key] = sheet;
            return sheet;
        }
    }

    private static bool TryFromCulture(CultureInfo culture, out GameDataClientLanguage lang)
    {
        switch (culture.TwoLetterISOLanguageName?.ToLowerInvariant())
        {
            case "en": lang = GameDataClientLanguage.English; return true;
            case "ja": lang = GameDataClientLanguage.Japanese; return true;
            case "de": lang = GameDataClientLanguage.German; return true;
            case "fr": lang = GameDataClientLanguage.French; return true;
            default: lang = default; return false;
        }
    }

    private static GameDataClientLanguage FromDalamud(Dalamud.Game.ClientLanguage lang) => lang switch
    {
        Dalamud.Game.ClientLanguage.Japanese => GameDataClientLanguage.Japanese,
        Dalamud.Game.ClientLanguage.German   => GameDataClientLanguage.German,
        Dalamud.Game.ClientLanguage.French   => GameDataClientLanguage.French,
        _                                    => GameDataClientLanguage.English,
    };

    private static Dalamud.Game.ClientLanguage ToDalamud(GameDataClientLanguage lang) => lang switch
    {
        GameDataClientLanguage.Japanese => Dalamud.Game.ClientLanguage.Japanese,
        GameDataClientLanguage.German   => Dalamud.Game.ClientLanguage.German,
        GameDataClientLanguage.French   => Dalamud.Game.ClientLanguage.French,
        _                               => Dalamud.Game.ClientLanguage.English,
    };
}
