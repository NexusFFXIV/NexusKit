namespace NexusKit.Sync.Contracts;

/// <summary>What a peer does with one collection, which decides what can break it.</summary>
public enum CollectionRole
{
    /// <summary>The key carries no scope for it, so nothing that happens to it matters.</summary>
    Unused,

    /// <summary>Uplink: the peer sends records. It must be able to produce what the server stores.</summary>
    Writer,

    /// <summary>Downlink: the peer reads records. It must be able to consume what the server sends.</summary>
    Reader,
}

/// <summary>Whether a peer can work from a given contract version, and if not, what stopped it.</summary>
/// <param name="IsSupported">True when the peer can use the offered version as it stands.</param>
/// <param name="Blockers">Every reason it cannot, in detection order. Empty when supported.</param>
public sealed record SupportResult(bool IsSupported, IReadOnlyList<string> Blockers)
{
    /// <summary>A clean result.</summary>
    public static SupportResult Supported { get; } = new(true, []);

    /// <summary>All blockers on one line, for logs and portal display.</summary>
    public override string ToString() =>
        IsSupported ? "supported" : string.Join("; ", Blockers);
}

/// <summary>
/// Whether a peer built against one contract version can speak another.
/// <para>A different question from <see cref="ContractCompatibility"/>, and that difference is the
/// point. Compatibility asks whether a version may be <i>registered</i> — one answer for everybody.
/// This asks whether <i>this</i> peer can use it, and the answer depends on what the peer does with
/// each collection: the same change is harmless for something that writes and fatal for something
/// that reads. Removing a field costs a writer nothing and costs a reader the data.</para>
/// <para>Lives here, beside the norm both sides reference, so that a client choosing a version and a
/// server explaining why a client is behind reach the same verdict from the same code rather than
/// from two implementations that agree until the first edit.</para>
/// </summary>
public static class ClientSupport
{
    /// <summary>
    /// Whether a peer built against <paramref name="clientBuild"/> can work from
    /// <paramref name="serverOffers"/>.
    /// <para>Judged per collection, not per contract: a version that breaks one collection the peer
    /// actually uses is unusable, however well the rest of it fits.</para>
    /// </summary>
    /// <param name="clientBuild">The document the peer was built against.</param>
    /// <param name="serverOffers">The version being considered.</param>
    /// <param name="grantedScopes">
    /// The peer's bare scopes, as handed back by the handshake — <c>reports:push</c>,
    /// <c>items:pull</c>. This is what separates a collection the peer uses from one it merely
    /// knows about, and without it every declared collection would have to be assumed in use,
    /// holding peers back over collections their key cannot even touch.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static SupportResult Evaluate(
        SyncContract clientBuild,
        SyncContract serverOffers,
        IReadOnlySet<string> grantedScopes)
    {
        ArgumentNullException.ThrowIfNull(clientBuild);
        ArgumentNullException.ThrowIfNull(serverOffers);
        ArgumentNullException.ThrowIfNull(grantedScopes);

        var blockers = new List<string>();

        if (!string.Equals(clientBuild.ContractId, serverOffers.ContractId, StringComparison.Ordinal))
        {
            blockers.Add(
                $"Contract id is '{serverOffers.ContractId}', but this peer was built against "
                + $"'{clientBuild.ContractId}'.");

            return new SupportResult(false, blockers);
        }

        if (clientBuild.Version.Major != serverOffers.Version.Major)
        {
            // Majors are where breaking changes live. A peer does not cross one by choosing to.
            blockers.Add(
                $"Version {serverOffers.Version} is a different major from {clientBuild.Version}; "
                + "crossing a major needs a rebuilt peer, not a negotiation.");

            return new SupportResult(false, blockers);
        }

        foreach (var mine in clientBuild.Collections)
        {
            var role = RoleOf(mine, grantedScopes);
            if (role == CollectionRole.Unused) continue;

            EvaluateCollection(mine, serverOffers.FindCollection(mine.Name), role, blockers);
        }

        // Collections the offered version adds are never a problem: this peer has no scope for
        // them and no code that mentions them, so it will not notice they exist.

        return blockers.Count == 0 ? SupportResult.Supported : new SupportResult(false, blockers);
    }

    /// <summary>
    /// What the peer does with a collection: whichever of push or pull its key was granted.
    /// <para>Direction decides the verb and a collection has exactly one, so a collection is a
    /// writer or a reader, never both. A peer that does both does it across <i>different</i>
    /// collections, and each is judged on its own terms.</para>
    /// </summary>
    private static CollectionRole RoleOf(CollectionDefinition collection, IReadOnlySet<string> grantedScopes)
    {
        if (!grantedScopes.Contains(ContractScopes.For(collection))) return CollectionRole.Unused;

        return collection.Direction == SyncDirection.Uplink ? CollectionRole.Writer : CollectionRole.Reader;
    }

    private static void EvaluateCollection(
        CollectionDefinition mine,
        CollectionDefinition? theirs,
        CollectionRole role,
        List<string> blockers)
    {
        var where = $"Collection '{mine.Name}' ({role.ToString().ToLowerInvariant()})";

        if (theirs is null)
        {
            // The one place the two roles diverge outright rather than by argument order. A writer
            // loses nothing by having nowhere to send; a reader loses the data.
            if (role == CollectionRole.Reader)
                blockers.Add($"{where} is gone from this version, and its records with it.");

            return;
        }

        if (mine.Direction != theirs.Direction)
        {
            blockers.Add(
                $"{where} now flows {theirs.Direction} instead of {mine.Direction}. A collection "
                + "that reverses direction is a different collection, not a later one.");

            return;
        }

        if (!string.Equals(mine.Key, theirs.Key, StringComparison.Ordinal))
        {
            blockers.Add(
                $"{where} is keyed on '{theirs.Key}' instead of '{mine.Key}', so the records this "
                + "peer addresses are not the ones it would reach.");

            return;
        }

        // Every field this peer uses must have a counterpart that can carry it. Which way the
        // values travel is the whole difference between the roles, so it is the only thing that
        // changes: a writer's values go from its declaration into the server's, a reader's come
        // back the other way.
        foreach (var mineField in mine.Fields)
        {
            var theirsField = theirs.FindField(mineField.Name);

            if (theirsField is null)
            {
                if (role == CollectionRole.Reader)
                {
                    // Even an optional field: absence is legal for it, but the peer declared the
                    // field in order to use it, and it is not coming back.
                    blockers.Add($"{where} no longer carries field '{mineField.Name}'.");
                }

                continue;
            }

            var (from, to) = role == CollectionRole.Writer
                ? (mineField, theirsField)
                : (theirsField, mineField);

            if (WhyCannotCarry(from, to) is { } reason)
                blockers.Add($"{where}, field '{mineField.Name}': {reason}");
        }

        if (role != CollectionRole.Writer) return;

        // Only a writer can be stopped by a field being added, and only a required one: it has to
        // supply a value for something it has never heard of. A reader simply ignores the extra.
        foreach (var theirsField in theirs.Fields)
        {
            if (!theirsField.Required) continue;
            if (mine.FindField(theirsField.Name) is not null) continue;

            blockers.Add(
                $"{where} requires field '{theirsField.Name}', which this peer does not know and "
                + "cannot fill.");
        }
    }

    /// <summary>
    /// Why values declared as <paramref name="from"/> cannot be carried as
    /// <paramref name="to"/>, or null when they can.
    /// <para>Direction-free on purpose. Both roles ask exactly this, with the arguments the other
    /// way round, which is why there is one function here and not two lists of rules that would
    /// have to be kept in agreement.</para>
    /// </summary>
    private static string? WhyCannotCarry(FieldDefinition from, FieldDefinition to)
    {
        if (to.Required && !from.Required)
            return $"required as {to.Type}, but the other side may omit it.";

        return FieldTypeConversion.Between(from, to) switch
        {
            ConversionKind.Widening => null,

            // A narrowing is a blocker here even though the registry allows it. The registry can
            // ask the stored records whether they fit; a peer facing a value it has not seen yet
            // cannot, so for it "possible for some values" is not good enough.
            ConversionKind.Narrowing =>
                $"{from.Type} does not always fit {to.Type} (or the bounds tightened).",

            _ => $"{from.Type} cannot be carried as {to.Type} at all.",
        };
    }
}
