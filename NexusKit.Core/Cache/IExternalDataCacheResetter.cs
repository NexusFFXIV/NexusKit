namespace NexusKit.Core.Cache;

/// <summary>
/// Bundle of every identifier a player can appear under across our
/// external-data caches. The caller fills in what it knows from the
/// in-game observation (always the Lodestone id once resolved; name and
/// world only when it has a fresh observation row to read them from) and
/// hands the same context to every registered resetter; each module
/// picks the subset it actually needs.
/// <para>The Lodestone module needs the name+world pair on top of the id
/// because its search-cache is keyed on <c>(name, world)</c> — without
/// it, a poisoned empty search row would survive the heal and keep
/// pinning the LodestoneId-resolution path into a fast-fail loop.</para>
/// </summary>
public sealed class ResetContext
{
    public ResetContext(ulong lodestoneId, string? name = null, string? homeWorldName = null)
    {
        LodestoneId = lodestoneId;
        Name = name;
        HomeWorldName = homeWorldName;
    }

    public ulong LodestoneId { get; }
    public string? Name { get; }
    public string? HomeWorldName { get; }
}

/// <summary>
/// Per-module surface for evicting every cache row associated with a
/// player. Each external-data module that owns a player-keyed cache table
/// (Lodestone HTML, FFXIVCollect responses, …) registers one
/// implementation; consumers that want a total cache flush for a player —
/// e.g. <c>PlayerRefreshQueueService</c>'s self-heal path when a fast-fail
/// loop signals cache poisoning — drain every registered resetter via
/// <c>IEnumerable&lt;IExternalDataCacheResetter&gt;</c>.
/// <para>The cache-key shape stays internal to each implementing module;
/// callers pass the <see cref="ResetContext"/> and let the module decide
/// which rows to delete.</para>
/// </summary>
public interface IExternalDataCacheResetter
{
    /// <summary>Delete every cache row this module knows about for the
    /// player described by <paramref name="ctx"/>. Returns the number of
    /// rows removed — zero is a normal outcome when the cache had nothing
    /// for the player yet.</summary>
    Task<int> ResetAsync(ResetContext ctx, CancellationToken ct = default);
}
