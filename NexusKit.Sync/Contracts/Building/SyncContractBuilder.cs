namespace NexusKit.Sync.Contracts.Building;

/// <summary>
/// Builds a <see cref="SyncContract"/> from CLR types.
/// <para>This is a convenience, not the source of truth. The authority is the canonical JSON
/// document a server registers; this builder exists so a plugin author can keep type safety
/// on the client side and still be certain the two describe the same thing, because the
/// document is generated from the same declaration the code compiles against.</para>
/// <para>An author who would rather write the JSON by hand loses nothing but the typed
/// accessors — <see cref="ContractJson.Parse"/> produces an equally valid contract.</para>
/// </summary>
/// <example>
/// <code>
/// public static readonly SyncContract Contract = SyncContract
///     .Define("acme.venuetracker", "1.0")
///     .Uplink&lt;VenueReport&gt;("reports", c => c
///         .Key(x => x.VenueId)
///         .Field(x => x.Rating, f => f.Range(1, 5))
///         .Indexed(x => x.VenueId)
///         .RateLimit(perMinute: 60)
///         .Retention(TimeSpan.FromDays(180)))
///     .Downlink&lt;Venue&gt;("venues", c => c.Key(x => x.Id))
///     .Build();
/// </code>
/// </example>
public sealed class SyncContractBuilder
{
    private readonly string mContractId;
    private readonly ContractVersion mVersion;
    private readonly List<CollectionDefinition> mCollections = [];

    internal SyncContractBuilder(string contractId, ContractVersion version)
    {
        mContractId = contractId;
        mVersion = version;
    }

    /// <summary>
    /// Declares a client-to-server collection: what the plugin collects while the game runs.
    /// </summary>
    /// <param name="name">Collection name — lowercase letters, digits and underscores.</param>
    /// <param name="configure">Optional refinement of the inferred fields.</param>
    public SyncContractBuilder Uplink<T>(string name, Action<CollectionBuilder<T>>? configure = null) =>
        Add(name, SyncDirection.Uplink, configure);

    /// <summary>
    /// Declares a server-to-client collection: what an author curates outside the game and
    /// clients mirror locally.
    /// </summary>
    /// <param name="name">Collection name — lowercase letters, digits and underscores.</param>
    /// <param name="configure">Optional refinement of the inferred fields.</param>
    public SyncContractBuilder Downlink<T>(string name, Action<CollectionBuilder<T>>? configure = null) =>
        Add(name, SyncDirection.Downlink, configure);

    /// <summary>Validates and produces the contract.</summary>
    /// <exception cref="ContractDefinitionException">The resulting document is invalid.</exception>
    public SyncContract Build() => SyncContract.Create(mContractId, mVersion, mCollections);

    private SyncContractBuilder Add<T>(string name, SyncDirection direction, Action<CollectionBuilder<T>>? configure)
    {
        var builder = new CollectionBuilder<T>(name, direction);
        configure?.Invoke(builder);
        mCollections.Add(builder.Build());
        return this;
    }
}
