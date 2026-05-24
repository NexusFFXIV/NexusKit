using Lumina.Excel;

namespace NexusKit.GameData;

/// <summary>
/// Raw passthrough to Lumina excel sheets. Use this when <see cref="IGameDataLookups"/>
/// doesn't cover the sheet you need; otherwise prefer the typed helpers.
/// </summary>
public interface ISheetsProvider
{
    /// <summary>Default language used when callers don't pass an explicit override.
    /// Tracks the framework's <c>LocalizationManager</c> when available, otherwise
    /// falls back to Dalamud's UI language.</summary>
    GameDataClientLanguage CurrentLanguage { get; }

    /// <summary>Return the Lumina sheet for <typeparamref name="T"/>. Cached per
    /// (type, language). Null if the sheet isn't shipped by the game data files.</summary>
    ExcelSheet<T>? GetSheet<T>(GameDataClientLanguage? language = null)
        where T : struct, IExcelRow<T>;
}
