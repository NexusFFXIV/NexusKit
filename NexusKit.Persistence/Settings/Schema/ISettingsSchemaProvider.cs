namespace NexusKit.Persistence.Settings.Schema;

public interface ISettingsSchemaProvider
{
    IReadOnlyList<IRegisteredSettingsSchema> All { get; }
}
