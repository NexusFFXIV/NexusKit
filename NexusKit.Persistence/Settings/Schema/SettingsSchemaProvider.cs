namespace NexusKit.Persistence.Settings.Schema;

internal sealed class SettingsSchemaProvider : ISettingsSchemaProvider
{
    public IReadOnlyList<IRegisteredSettingsSchema> All { get; }

    public SettingsSchemaProvider(IEnumerable<IRegisteredSettingsSchema> schemas)
    {
        All = schemas.ToList();
    }
}
