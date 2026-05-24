namespace NexusKit.GameData;

/// <summary>Slim record returned by <see cref="IGameDataLookups"/> world enumerations.</summary>
public sealed record WorldInfo(uint RowId, string Name, uint DataCenterId);
