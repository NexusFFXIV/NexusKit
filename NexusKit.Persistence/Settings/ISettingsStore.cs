namespace NexusKit.Persistence.Settings;

public interface ISettingsStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
