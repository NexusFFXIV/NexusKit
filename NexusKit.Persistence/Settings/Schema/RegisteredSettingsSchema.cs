using NexusKit.Core.Localization;

namespace NexusKit.Persistence.Settings.Schema;

internal sealed class RegisteredSettingsSchema<T> : IRegisteredSettingsSchema
    where T : class, new()
{
    public Type SettingsType => typeof(T);
    public string StoreKey { get; }
    public LocalizedText Group { get; }
    public int GroupOrder { get; }
    public LocalizedText Title { get; }
    public IReadOnlyList<SettingsPropertyDescriptor> Properties { get; }

    public RegisteredSettingsSchema(
        string storeKey,
        LocalizedText group,
        int groupOrder,
        LocalizedText title,
        IReadOnlyList<SettingsPropertyDescriptor> properties)
    {
        StoreKey = storeKey;
        Group = group;
        GroupOrder = groupOrder;
        Title = title;
        Properties = properties;
    }

    public async Task<object> LoadAsync(ISettingsStore store, CancellationToken ct = default)
    {
        var poco = await store.GetAsync<T>(StoreKey, ct).ConfigureAwait(false);
        return poco ?? new T();
    }

    public Task SaveAsync(ISettingsStore store, object instance, CancellationToken ct = default)
    {
        return store.SetAsync(StoreKey, (T)instance, ct);
    }
}
