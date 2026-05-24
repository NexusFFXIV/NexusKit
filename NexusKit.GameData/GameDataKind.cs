namespace NexusKit.GameData;

/// <summary>
/// The categories that <see cref="IGameDataResolver.ResolveIdByName"/> understands.
/// Each kind maps internally to a specific Lumina sheet (Mount → <c>Mount</c>,
/// Minion → <c>Companion</c>, Item → <c>Item</c>, World → <c>World</c>,
/// ClassJob → <c>ClassJob</c>, Ornament → <c>Ornament</c>).
/// <para>Persistence-side guidance: external modules should resolve the API-supplied
/// string to a RowId via this resolver and store the RowId — the localized display
/// name can always be recovered later via <see cref="IGameDataLookups"/>, so the
/// database stays multi-language capable without storing strings.</para>
/// </summary>
public enum GameDataKind
{
    Mount,
    Minion,
    Item,
    World,
    ClassJob,
    Ornament,
    GrandCompany,
    DataCenter,
    HousingDistrict,
}