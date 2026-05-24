using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace NexusKit.Persistence.Settings;

internal sealed class SettingsStore : ISettingsStore
{
    private readonly INexusDbContextFactory mFactory;
    private readonly ConcurrentDictionary<string, string?> mCache = new();

    public SettingsStore(INexusDbContextFactory factory)
    {
        mFactory = factory;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (!mCache.TryGetValue(key, out var json))
        {
            await using var ctx = await mFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var row = await ctx.Set<SettingsEntity>().FindAsync([key], cancellationToken: ct).ConfigureAwait(false);
            json = row?.Value;
            mCache[key] = json;
        }

        return json is null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);

        await using var ctx = await mFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await ctx.Set<SettingsEntity>().FindAsync([key], cancellationToken: ct).ConfigureAwait(false);
        if (existing is null)
        {
            ctx.Set<SettingsEntity>().Add(new SettingsEntity
            {
                Key = key,
                Value = json,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Value = json;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

        mCache[key] = json;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await using var ctx = await mFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await ctx.Set<SettingsEntity>().FindAsync([key], cancellationToken: ct).ConfigureAwait(false);
        if (existing is not null)
        {
            ctx.Set<SettingsEntity>().Remove(existing);
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        mCache.TryRemove(key, out _);
    }
}
