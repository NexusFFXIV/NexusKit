using NexusKit.Core.Localization;

namespace NexusKit.Persistence.Settings.Schema;

public interface IRegisteredSettingsSchema
{
    Type SettingsType { get; }
    string StoreKey { get; }
    LocalizedText Group { get; }
    int GroupOrder { get; }
    LocalizedText Title { get; }
    IReadOnlyList<SettingsPropertyDescriptor> Properties { get; }

    Task<object> LoadAsync(ISettingsStore store, CancellationToken ct = default);
    Task SaveAsync(ISettingsStore store, object instance, CancellationToken ct = default);
}
