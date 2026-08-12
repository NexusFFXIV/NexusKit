namespace NexusKit.Sync.Contracts;

/// <summary>
/// How much of a field's value space survives a type change.
/// </summary>
public enum ConversionKind
{
    /// <summary>
    /// No conversion exists. <c>guid → integer</c> is not a lossy conversion, it is not a
    /// conversion at all, and no amount of inspecting the stored data would make it one.
    /// </summary>
    None,

    /// <summary>
    /// Every value of the source is a value of the target. Cannot fail on any input, so it
    /// needs no knowledge of what is stored.
    /// </summary>
    Widening,

    /// <summary>
    /// Some values convert and some do not. Whether <i>these</i> values do is a question about
    /// the data, not about the types — ask the storage layer, not this class.
    /// </summary>
    Narrowing,
}

/// <summary>
/// Whether one field declaration's values can be carried by another.
/// <para>The relation both halves of the system need and neither had. A server deciding whether
/// a new contract version may be registered, and a client deciding whether it can speak that
/// version, must reach the <b>same</b> verdict — so the rule lives here, in the norm both
/// reference, rather than twice in two codebases that would drift apart on the first edit.</para>
/// <para>The two-step split is the whole design. <see cref="ConversionKind.Widening"/> is safe on
/// its own; <see cref="ConversionKind.Narrowing"/> is a question this class deliberately refuses
/// to answer, because a type table cannot know that a column holds only 0 and 1 and therefore
/// converts cleanly to a boolean. Returning a bare "yes" for those would be a guess dressed up as
/// a rule.</para>
/// </summary>
public static class FieldTypeConversion
{
    // Worst-case rendered length of each type as a JSON string, so that a conversion *to* a
    // length-capped string can tell a genuine widening from one that merely looks like it.
    private const int GuidLength = 36;         // 8-4-4-4-12
    private const int TimestampLength = 33;    // 2026-08-11T13:37:51.1234567+02:00
    private const int IntegerLength = 20;      // -9223372036854775808
    private const int NumberLength = 31;       // sign, leading digit, point, 28 more digits
    private const int BooleanLength = 5;       // "false"

    /// <summary>
    /// The type table alone, ignoring any constraints the declarations carry.
    /// <para>Identical types report <see cref="ConversionKind.Widening"/>: the identity is
    /// lossless, and saying so here means no caller needs a special case for "nothing
    /// changed".</para>
    /// </summary>
    /// <param name="from">The type values are coming from.</param>
    /// <param name="to">The type they must fit into.</param>
    public static ConversionKind Between(FieldType from, FieldType to)
    {
        if (from == to) return ConversionKind.Widening;

        return (from, to) switch
        {
            // Lossless. Every value of the source has an exact counterpart in the target, so
            // nothing needs to be inspected before allowing it.
            (FieldType.Integer, FieldType.Number) => ConversionKind.Widening,
            (FieldType.Integer, FieldType.String) => ConversionKind.Widening,
            (FieldType.Number, FieldType.String) => ConversionKind.Widening,
            (FieldType.Boolean, FieldType.String) => ConversionKind.Widening,
            (FieldType.Guid, FieldType.String) => ConversionKind.Widening,
            (FieldType.Timestamp, FieldType.String) => ConversionKind.Widening,

            // Possible in principle, and only in principle. Each of these fails on some value,
            // which is why the answer stops here and the data gets the deciding vote.
            (FieldType.Number, FieldType.Integer) => ConversionKind.Narrowing,
            (FieldType.Integer, FieldType.Boolean) => ConversionKind.Narrowing,
            (FieldType.String, FieldType.Integer) => ConversionKind.Narrowing,
            (FieldType.String, FieldType.Number) => ConversionKind.Narrowing,
            (FieldType.String, FieldType.Boolean) => ConversionKind.Narrowing,
            (FieldType.String, FieldType.Guid) => ConversionKind.Narrowing,
            (FieldType.String, FieldType.Timestamp) => ConversionKind.Narrowing,

            // Everything else, including number → boolean and boolean → integer. Both are
            // reachable across two minors via string, and both are left out on purpose: this
            // table is the authority on what one step may do, and it earns that by staying
            // small enough to read in one sitting.
            _ => ConversionKind.None,
        };
    }

    /// <summary>
    /// Whether every value <paramref name="from"/> permits is a value <paramref name="to"/>
    /// permits — type and constraints together. This is the overload callers want.
    /// <para>Constraints are part of the answer, not an afterthought. A <c>guid → string</c> whose
    /// target caps length at 20 is not a widening however the type table reads it, and a
    /// <c>string(64) → string(32)</c> narrows without any type changing at all. Reporting those
    /// as <see cref="ConversionKind.Narrowing"/> sends them to the one place that can settle
    /// them: the stored data.</para>
    /// </summary>
    /// <param name="from">The declaration values are coming from.</param>
    /// <param name="to">The declaration they must fit into.</param>
    /// <exception cref="ArgumentNullException">Either declaration is null.</exception>
    public static ConversionKind Between(FieldDefinition from, FieldDefinition to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var byType = Between(from.Type, to.Type);
        if (byType == ConversionKind.None) return ConversionKind.None;

        // A tightened constraint on the target demotes a widening; it can never rescue a
        // narrowing, so there is nothing to do once we are already there.
        if (byType == ConversionKind.Narrowing) return ConversionKind.Narrowing;

        return Tightens(from, to) ? ConversionKind.Narrowing : ConversionKind.Widening;
    }

    /// <summary>
    /// Whether the target's constraints exclude values the source allowed.
    /// </summary>
    private static bool Tightens(FieldDefinition from, FieldDefinition to)
    {
        if (to.Type == FieldType.String && to.MaxLength is { } cap && cap < LongestValueOf(from))
            return true;

        // Only reachable for integer → number and number → number: every other pairing that
        // gets this far has a non-numeric target, where Min/Max carry no meaning.
        if (to.Type is FieldType.Integer or FieldType.Number)
        {
            if (to.Min is { } min && (from.Min is null || min > from.Min)) return true;
            if (to.Max is { } max && (from.Max is null || max < from.Max)) return true;
        }

        return false;
    }

    /// <summary>
    /// The longest string a value of this declaration can render as, or
    /// <see cref="int.MaxValue"/> when the declaration puts no bound on it.
    /// </summary>
    private static int LongestValueOf(FieldDefinition field) => field.Type switch
    {
        FieldType.String => field.MaxLength ?? int.MaxValue,
        FieldType.Integer => IntegerLength,
        FieldType.Number => NumberLength,
        FieldType.Boolean => BooleanLength,
        FieldType.Guid => GuidLength,
        FieldType.Timestamp => TimestampLength,

        // Unreachable while FieldType has six members, and deliberately pessimistic rather than
        // a throw: a seventh member added without visiting this switch should make conversions
        // look impossible, not make a caller believe an unexamined type converts cleanly.
        _ => int.MaxValue,
    };
}
