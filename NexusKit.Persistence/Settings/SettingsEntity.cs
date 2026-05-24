namespace NexusKit.Persistence.Settings;

public sealed class SettingsEntity
{
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}
