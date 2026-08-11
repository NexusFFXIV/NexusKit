namespace NexusKit.Sync.Contracts;

/// <summary>
/// One field of a <see cref="CollectionDefinition"/>.
/// <para>Constraints declared here are enforced <b>server-side</b> on every write. A client
/// may check them too for a better error message, but it is never the authority: the whole
/// point of putting them in the contract is that a forged or outdated client cannot write
/// something the contract forbids.</para>
/// </summary>
public sealed record FieldDefinition
{
    /// <summary>
    /// Field name. Lowercase letters, digits and underscores, starting with a letter — see
    /// <see cref="ContractNames"/>. Becomes a JSON key and a storage path, so it is
    /// deliberately narrower than what JSON itself would allow.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>The value type this field carries.</summary>
    public required FieldType Type { get; init; }

    /// <summary>
    /// When true, a payload missing this field (or carrying JSON <c>null</c> for it) is
    /// rejected. Defaults to false, because making a field required later is a breaking
    /// change and the safer default is the one you can tighten only with a new major.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Inclusive lower bound. Only meaningful for <see cref="FieldType.Integer"/> and
    /// <see cref="FieldType.Number"/>.
    /// </summary>
    public decimal? Min { get; init; }

    /// <summary>Inclusive upper bound. Same applicability as <see cref="Min"/>.</summary>
    public decimal? Max { get; init; }

    /// <summary>
    /// Maximum length in UTF-16 code units. Only meaningful for <see cref="FieldType.String"/>.
    /// An unbounded string field is a standing invitation to fill someone's database, so
    /// declaring this is strongly encouraged even when the contract does not force it.
    /// </summary>
    public int? MaxLength { get; init; }
}
