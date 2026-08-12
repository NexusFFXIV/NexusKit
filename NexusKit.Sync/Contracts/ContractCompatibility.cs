using System.Globalization;

namespace NexusKit.Sync.Contracts;

/// <summary>
/// A type change that is permitted but can fail on individual stored values.
/// <para>Carried structurally rather than as prose because something has to act on it: the
/// registry asks the storage layer whether <i>these</i> records survive the conversion, and it
/// cannot do that by parsing an English sentence back apart.</para>
/// </summary>
/// <param name="Collection">The collection the field belongs to.</param>
/// <param name="From">The declaration as registered.</param>
/// <param name="To">The declaration being registered — the one stored values must satisfy.</param>
public sealed record NarrowingConversion(string Collection, FieldDefinition From, FieldDefinition To);

/// <summary>The outcome of comparing a candidate contract version against the registered one.</summary>
/// <param name="IsCompatible">True when the candidate may be registered as a new minor.</param>
/// <param name="BreakingChanges">Every incompatibility found, in detection order.</param>
/// <param name="Notes">
/// Changes that are allowed but that somebody should see — chiefly type conversions. Separate from
/// <paramref name="BreakingChanges"/> because a result that is compatible still has something to
/// say, and there was previously nowhere to say it.
/// </param>
/// <param name="Narrowings">
/// The subset of <paramref name="Notes"/> that can fail on stored data, in a form the storage
/// layer can act on.
/// </param>
public sealed record CompatibilityResult(
    bool IsCompatible,
    IReadOnlyList<string> BreakingChanges,
    IReadOnlyList<string> Notes,
    IReadOnlyList<NarrowingConversion> Narrowings)
{
    /// <summary>A clean result.</summary>
    public static CompatibilityResult Compatible { get; } = new(true, [], [], []);

    /// <summary>All breaking changes on one line, for logs and error payloads.</summary>
    public override string ToString() =>
        IsCompatible ? "compatible" : string.Join("; ", BreakingChanges);
}

/// <summary>
/// Decides whether a new contract version can replace the registered one without breaking
/// peers that still speak the old one.
/// <para>The model is the one schema registries settled on: within a major version, evolution
/// must be <b>additive</b>. Anything that could make a previously valid record invalid, or a
/// previously readable record unreadable, needs a new major — because there is no moment at
/// which every deployed client updates at once, and a change that assumes there is will break
/// whoever updates last.</para>
/// </summary>
public static class ContractCompatibility
{
    /// <summary>
    /// Checks whether <paramref name="candidate"/> may be registered alongside
    /// <paramref name="registered"/>.
    /// </summary>
    /// <param name="registered">The version already known to the server.</param>
    /// <param name="candidate">The version being registered.</param>
    public static CompatibilityResult Check(SyncContract registered, SyncContract candidate)
    {
        ArgumentNullException.ThrowIfNull(registered);
        ArgumentNullException.ThrowIfNull(candidate);

        var found = new Findings();

        if (!string.Equals(registered.ContractId, candidate.ContractId, StringComparison.Ordinal))
        {
            // Not an evolution at all — these are two different contracts.
            found.Breaking.Add(
                $"Contract id changed from '{registered.ContractId}' to '{candidate.ContractId}'. "
                + "A different id is a different contract, not a new version of this one.");

            return found.ToResult();
        }

        if (registered.Version.Major != candidate.Version.Major)
        {
            // A new major is *allowed* — it is how breaking changes ship — it just is not a
            // compatible evolution of this one, and both versions live side by side.
            found.Breaking.Add(
                $"Major version changed from {registered.Version.Major} to {candidate.Version.Major}. "
                + "Register it as a separate major; it does not supersede the existing one.");

            return found.ToResult();
        }

        if (candidate.Version <= registered.Version)
        {
            found.Breaking.Add(
                $"Version {candidate.Version} does not advance on the registered {registered.Version}.");
        }

        foreach (var old in registered.Collections)
        {
            var updated = candidate.FindCollection(old.Name);
            if (updated is null)
            {
                found.Breaking.Add($"Collection '{old.Name}' was removed; peers still using it would stop working.");
                continue;
            }

            CompareCollection(old, updated, found);
        }

        // New collections are additive by construction: nothing that exists today refers to
        // them, so nothing that exists today can break on them.

        return found.ToResult();
    }

    /// <summary>
    /// Checks <paramref name="candidate"/> against <b>every</b> version still registered, not only
    /// the newest.
    /// <para>Necessary because conversions do not have to be transitive. <c>integer → number →
    /// string</c> passes as two separate steps while <c>integer → string</c> could have been
    /// refused outright, and a peer that never left the first version would then be handed data it
    /// cannot read — with every individual registration having been approved. Comparing only
    /// against the highest minor cannot see that; comparing against all of them can.</para>
    /// <para>Pass every registered version of the same major. Other majors may be included and are
    /// harmless — each is reported as its own incompatibility, which is what they are.</para>
    /// </summary>
    /// <param name="registered">The versions already known to the server, in any order.</param>
    /// <param name="candidate">The version being registered.</param>
    public static CompatibilityResult CheckAll(IEnumerable<SyncContract> registered, SyncContract candidate)
    {
        ArgumentNullException.ThrowIfNull(registered);
        ArgumentNullException.ThrowIfNull(candidate);

        // Oldest first, so a reader of the log walks the history in the order it happened.
        var predecessors = registered.OrderBy(c => c.Version).ToArray();
        if (predecessors.Length == 0) return CompatibilityResult.Compatible;

        var found = new Findings();
        var scanned = new HashSet<(string Collection, string Field)>();

        foreach (var predecessor in predecessors)
        {
            var one = Check(predecessor, candidate);

            // Which version it broke against is the actionable half of the message: "field 'x'
            // changed type" is a puzzle, "vs 1.0: field 'x' changed type" is an instruction.
            var against = $"vs {predecessor.Version}: ";

            foreach (var change in one.BreakingChanges) found.Breaking.Add(against + change);
            foreach (var note in one.Notes) found.Notes.Add(against + note);

            // Deduplicated on the target field, because the scan a narrowing triggers depends only
            // on the declaration values must land in. Three predecessors narrowing into the same
            // field is one question about the data, asked once.
            foreach (var narrowing in one.Narrowings)
            {
                if (scanned.Add((narrowing.Collection, narrowing.To.Name)))
                    found.Narrowings.Add(narrowing);
            }
        }

        return found.ToResult();
    }

    private static void CompareCollection(CollectionDefinition old, CollectionDefinition updated, Findings found)
    {
        var where = $"Collection '{old.Name}'";

        if (old.Direction != updated.Direction)
        {
            found.Breaking.Add(
                $"{where} changed direction from {old.Direction} to {updated.Direction}. "
                + "Peers would be pushing to something that now only reads, or vice versa.");
        }

        if (!string.Equals(old.Key, updated.Key, StringComparison.Ordinal))
        {
            found.Breaking.Add(
                $"{where} changed its key from '{old.Key}' to '{updated.Key}'. "
                + "Existing records are addressed by the old key and would become unreachable.");
        }

        foreach (var oldField in old.Fields)
        {
            var newField = updated.FindField(oldField.Name);
            if (newField is null)
            {
                found.Breaking.Add($"{where} removed field '{oldField.Name}'.");
                continue;
            }

            CompareField(where, old.Name, oldField, newField, found);
        }

        foreach (var newField in updated.Fields)
        {
            if (old.FindField(newField.Name) is not null) continue;

            if (newField.Required)
            {
                // An older client has never heard of this field and cannot send it, so every
                // write it makes would now fail validation.
                found.Breaking.Add(
                    $"{where} added required field '{newField.Name}'. New fields must be optional; "
                    + "older peers cannot send a field they do not know about.");
            }
        }

        // Retention, rate limits and indexes deliberately do not appear here. They change how
        // the server treats data, not what a peer may send or read, so tightening them is an
        // operational decision rather than a compatibility break.
    }

    private static void CompareField(
        string where, string collection, FieldDefinition old, FieldDefinition updated, Findings found)
    {
        var what = $"{where}, field '{old.Name}'";

        if (old.Type != updated.Type)
        {
            var change = $"{what} changed type from {old.Type} to {updated.Type}";

            switch (FieldTypeConversion.Between(old, updated))
            {
                case ConversionKind.Widening:
                    // Allowed, and still worth saying out loud: peers that only *read* this
                    // collection now receive a wider type than they were built for, and whether
                    // they cope is a question about them, not about the stored records.
                    found.Notes.Add($"{change} — widening, no stored value can fail it.");
                    break;

                case ConversionKind.Narrowing:
                    found.Notes.Add(
                        $"{change} — narrowing, which holds only for values that fit the new type.");
                    found.Narrowings.Add(new NarrowingConversion(collection, old, updated));
                    break;

                default:
                    found.Breaking.Add(
                        $"{change}. No conversion exists between those types, so stored records "
                        + "would no longer match the declaration.");
                    break;
            }
        }

        // Presence, not range: no conversion can supply a value a peer never sends, so this
        // applies whether or not the type changed.
        if (!old.Required && updated.Required)
        {
            found.Breaking.Add(
                $"{what} became required. Peers built against the previous version may omit it, "
                + "so their writes would start failing.");
        }

        // Only when the type stayed put. A changed type has already been judged above by
        // FieldTypeConversion, which folds the target's constraints into its verdict — and it
        // does so with the right model of them. These comparisons assume both sides are the same
        // kind of thing, so across a type change they mislead: a Guid field carries no MaxLength
        // because MaxLength is meaningless for one, and reading that absence as "unbounded" would
        // report every guid → string as a tightening when the values are always 36 characters.
        if (old.Type != updated.Type) return;

        // Constraints may only loosen. A tightened bound turns records that were valid
        // yesterday into rejects today, which from a user's perspective is the feature
        // breaking rather than the contract evolving.
        if (updated.MaxLength is { } newMax && (old.MaxLength is null || newMax < old.MaxLength))
        {
            found.Breaking.Add(
                $"{what} tightened MaxLength from {Describe(old.MaxLength)} to {newMax}.");
        }

        if (updated.Min is { } newMin && (old.Min is null || newMin > old.Min))
            found.Breaking.Add($"{what} raised Min from {Describe(old.Min)} to {Number(newMin)}.");

        if (updated.Max is { } newMaxValue && (old.Max is null || newMaxValue < old.Max))
            found.Breaking.Add($"{what} lowered Max from {Describe(old.Max)} to {Number(newMaxValue)}.");
    }

    /// <summary>
    /// The three verdicts being accumulated during one comparison.
    /// <para>A single carrier rather than three list parameters threaded through every private
    /// method — the compare methods gained a third output and were about to gain a fifth
    /// parameter.</para>
    /// </summary>
    private sealed class Findings
    {
        public List<string> Breaking { get; } = [];

        public List<string> Notes { get; } = [];

        public List<NarrowingConversion> Narrowings { get; } = [];

        public CompatibilityResult ToResult() =>
            Breaking.Count == 0 && Notes.Count == 0
                ? CompatibilityResult.Compatible
                : new CompatibilityResult(Breaking.Count == 0, Breaking, Notes, Narrowings);
    }

    private static string Describe(int? value) =>
        value is { } v ? v.ToString(CultureInfo.InvariantCulture) : "unbounded";

    private static string Describe(decimal? value) =>
        value is { } v ? Number(v) : "unbounded";

    private static string Number(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
