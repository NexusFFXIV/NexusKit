using System.Globalization;
using NexusKit.Sync.Contracts.Building;

namespace NexusKit.Sync.Contracts;

/// <summary>
/// A validated contract: an identity, a version, and the collections it declares.
/// <para>Instances are immutable and can only be produced through <see cref="Create"/>, which
/// validates the whole document. Anything holding a <see cref="SyncContract"/> can therefore
/// assume it is structurally sound — no defensive re-checking scattered across the server.</para>
/// </summary>
public sealed class SyncContract : IEquatable<SyncContract>
{
    private readonly Lazy<string> mCanonicalJson;
    private readonly Lazy<string> mHash;

    private SyncContract(string contractId, ContractVersion version, IReadOnlyList<CollectionDefinition> collections)
    {
        ContractId = contractId;
        Version = version;
        Collections = collections;

        // Lazy because most contracts are constructed to be looked at, not hashed — the
        // server parses one per registration but hashes it only on handshake.
        mCanonicalJson = new Lazy<string>(() => ContractJson.Write(this));
        mHash = new Lazy<string>(() => ContractJson.ComputeHash(CanonicalJson));
    }

    /// <summary>Stable identity, e.g. <c>acme.venuetracker</c>.</summary>
    public string ContractId { get; }

    /// <summary>The <c>major.minor</c> version of this document.</summary>
    public ContractVersion Version { get; }

    /// <summary>The declared collections, ordered by name.</summary>
    public IReadOnlyList<CollectionDefinition> Collections { get; }

    /// <summary>
    /// The canonical JSON form — the exact bytes both peers hash. See <see cref="ContractJson"/>
    /// for the rules that make it reproducible.
    /// </summary>
    public string CanonicalJson => mCanonicalJson.Value;

    /// <summary>Lowercase hex SHA-256 of <see cref="CanonicalJson"/>.</summary>
    public string Hash => mHash.Value;

    /// <summary>Every scope this contract implies, ordered by collection name.</summary>
    public IReadOnlyList<string> Scopes => ContractScopes.All(this);

    /// <summary>Finds a collection by name, or null when it is not declared.</summary>
    public CollectionDefinition? FindCollection(string name)
    {
        foreach (var collection in Collections)
        {
            if (string.Equals(collection.Name, name, StringComparison.Ordinal)) return collection;
        }

        return null;
    }

    /// <summary>
    /// Starts a typed contract definition. See <see cref="SyncContractBuilder"/> for the full
    /// shape; the builder is a convenience over the canonical document, not a second authority.
    /// </summary>
    /// <param name="contractId">Stable identity, e.g. <c>acme.venuetracker</c>.</param>
    /// <param name="version">The <c>major.minor</c> version, e.g. <c>"1.0"</c>.</param>
    /// <exception cref="FormatException">The version is not a <c>major.minor</c> pair.</exception>
    public static SyncContractBuilder Define(string contractId, string version) =>
        new(contractId, ContractVersion.Parse(version));

    /// <summary>Starts a typed contract definition with an already-parsed version.</summary>
    public static SyncContractBuilder Define(string contractId, ContractVersion version) =>
        new(contractId, version);

    /// <summary>
    /// Validates and constructs a contract. Collections are sorted by name so that two
    /// authors declaring the same collections in a different order produce the same document
    /// and therefore the same hash.
    /// </summary>
    /// <exception cref="ContractDefinitionException">The document is structurally invalid.</exception>
    public static SyncContract Create(
        string contractId,
        ContractVersion version,
        IEnumerable<CollectionDefinition> collections)
    {
        ArgumentNullException.ThrowIfNull(collections);

        var problems = new List<string>();

        if (!ContractNames.IsValidContractId(contractId))
        {
            problems.Add(
                $"Contract id '{contractId}' is not valid. Expected at least two dot-separated lowercase "
                + $"segments (letters, digits, hyphens), each starting with a letter, e.g. 'acme.venuetracker'.");
        }

        if (version.Major < 0 || version.Minor < 0)
            problems.Add($"Contract version '{version}' is not valid: components cannot be negative.");

        var ordered = collections.OrderBy(c => c.Name, StringComparer.Ordinal).ToArray();

        if (ordered.Length == 0)
            problems.Add("A contract must declare at least one collection.");

        var seenCollections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var collection in ordered)
        {
            if (!seenCollections.Add(collection.Name))
                problems.Add($"Collection '{collection.Name}' is declared more than once.");

            ValidateCollection(collection, problems);
        }

        if (problems.Count > 0) throw new ContractDefinitionException(problems);

        return new SyncContract(contractId, version, ordered);
    }

    private static void ValidateCollection(CollectionDefinition collection, List<string> problems)
    {
        var where = $"Collection '{collection.Name}'";

        if (!ContractNames.IsValidName(collection.Name))
        {
            problems.Add(
                $"{where} has an invalid name. Expected lowercase letters, digits and underscores, "
                + "starting with a letter.");
        }

        if (collection.Fields.Count == 0)
        {
            problems.Add($"{where} declares no fields.");
            return;   // every remaining check reads Fields; no point piling on
        }

        var seenFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in collection.Fields)
        {
            if (!seenFields.Add(field.Name))
                problems.Add($"{where} declares field '{field.Name}' more than once.");

            ValidateField(collection, field, problems);
        }

        var key = collection.FindField(collection.Key);
        if (key is null)
        {
            problems.Add($"{where} names '{collection.Key}' as its key, but declares no such field.");
        }
        else
        {
            if (!key.Required)
            {
                // A record whose key may be absent cannot be addressed, deduplicated or
                // tombstoned — the whole storage model assumes the key is always there.
                problems.Add($"{where} uses '{key.Name}' as its key, so that field must be required.");
            }

            if (key.Type is not (FieldType.String or FieldType.Integer or FieldType.Guid))
            {
                problems.Add(
                    $"{where} uses '{key.Name}' of type {key.Type} as its key. Keys must be "
                    + "String, Integer or Guid — the other types have no stable textual identity.");
            }
        }

        var seenIndexed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var indexed in collection.Indexed)
        {
            if (!seenIndexed.Add(indexed))
                problems.Add($"{where} lists '{indexed}' as indexed more than once.");

            if (collection.FindField(indexed) is null)
                problems.Add($"{where} marks '{indexed}' as indexed, but declares no such field.");
        }

        if (collection.RateLimit is { PerMinute: <= 0 } limit)
            problems.Add($"{where} declares a rate limit of {limit.PerMinute}/minute; it must be positive.");

        if (collection.Retention is { } retention)
        {
            if (retention <= TimeSpan.Zero)
                problems.Add($"{where} declares a non-positive retention.");
            else if (retention.Ticks % TimeSpan.TicksPerSecond != 0)
                problems.Add($"{where} declares a retention with sub-second precision, which the wire format cannot carry.");
        }
    }

    private static void ValidateField(CollectionDefinition collection, FieldDefinition field, List<string> problems)
    {
        var where = $"Collection '{collection.Name}', field '{field.Name}'";

        if (!ContractNames.IsValidName(field.Name))
        {
            problems.Add(
                $"{where} has an invalid name. Expected lowercase letters, digits and underscores, "
                + "starting with a letter.");
        }

        var numeric = field.Type is FieldType.Integer or FieldType.Number;

        if (!numeric && (field.Min is not null || field.Max is not null))
            problems.Add($"{where} declares Min/Max, which only applies to Integer and Number fields.");

        if (field.Type != FieldType.String && field.MaxLength is not null)
            problems.Add($"{where} declares MaxLength, which only applies to String fields.");

        if (field.MaxLength is <= 0)
            problems.Add($"{where} declares MaxLength {field.MaxLength}; it must be positive.");

        if (field.Min is { } min && field.Max is { } max && min > max)
            problems.Add($"{where} declares Min {Number(min)} greater than Max {Number(max)}.");

        if (field.Type == FieldType.Integer)
        {
            if (field.Min is { } m && decimal.Truncate(m) != m)
                problems.Add($"{where} is an Integer field but declares a fractional Min of {Number(m)}.");

            if (field.Max is { } x && decimal.Truncate(x) != x)
                problems.Add($"{where} is an Integer field but declares a fractional Max of {Number(x)}.");
        }
    }

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Two contracts are equal when their canonical documents hash the same. Comparing the
    /// hash rather than the object graph is what makes equality mean the same thing here as
    /// it does on the wire.
    /// </summary>
    public bool Equals(SyncContract? other) =>
        other is not null && string.Equals(Hash, other.Hash, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as SyncContract);

    /// <inheritdoc />
    public override int GetHashCode() => Hash.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => $"{ContractId}@{Version}";
}
