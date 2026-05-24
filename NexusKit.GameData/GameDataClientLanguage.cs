namespace NexusKit.GameData;

/// <summary>
/// Dalamud-free wrapper for the four languages FFXIV / Lumina expose. Interfaces in
/// this kit project use it so consumers don't need a direct <c>Dalamud.Game.ClientLanguage</c>
/// reference; the provider implementation does the mapping internally.
/// </summary>
public enum GameDataClientLanguage
{
    English  = 0,
    Japanese = 1,
    German   = 2,
    French   = 3,
}
