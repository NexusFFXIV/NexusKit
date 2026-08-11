namespace NexusKit.Sync.Contracts;

/// <summary>
/// The value types a contract field may declare.
/// <para>Deliberately small. Every member maps onto exactly one JSON representation, which
/// is what lets a server validate a payload without knowing anything about the CLR types the
/// author used. Adding a member is a wire change: every implementation has to learn it.</para>
/// </summary>
public enum FieldType
{
    /// <summary>JSON string. Length constrained via <see cref="FieldDefinition.MaxLength"/>.</summary>
    String,

    /// <summary>
    /// JSON number without a fractional part, in <see cref="long"/> range. Bounded via
    /// <see cref="FieldDefinition.Min"/> / <see cref="FieldDefinition.Max"/>.
    /// </summary>
    Integer,

    /// <summary>JSON number, fractional part allowed.</summary>
    Number,

    /// <summary>JSON <c>true</c> / <c>false</c>.</summary>
    Boolean,

    /// <summary>
    /// JSON string holding an ISO-8601 instant with an explicit offset. Stored and compared
    /// as UTC — a payload without an offset is rejected rather than guessed at, because
    /// guessing turns a client's local midnight into a different instant per user.
    /// </summary>
    Timestamp,

    /// <summary>JSON string holding a GUID in the canonical 8-4-4-4-12 form.</summary>
    Guid,
}
