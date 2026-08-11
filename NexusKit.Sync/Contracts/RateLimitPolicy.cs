namespace NexusKit.Sync.Contracts;

/// <summary>
/// Per-collection write budget, enforced by the server against the calling API key.
/// <para>This is a contract-level declaration of what normal use looks like, not a security
/// control on its own — it bounds the damage a misbehaving or hostile client can do between
/// being noticed and being revoked.</para>
/// </summary>
/// <param name="PerMinute">
/// Maximum records accepted per minute per API key. Counted on records, not on requests, so
/// batching cannot be used to slip past it.
/// </param>
public sealed record RateLimitPolicy(int PerMinute);
