namespace NexusKit.Sync.Protocol;

/// <summary>
/// One page of changes.
/// </summary>
/// <param name="Changes">The changes, ordered by ascending sequence.</param>
/// <param name="NextCursor">
/// The cursor to pass as <see cref="PullRequest.Since"/> next time.
/// <para>Take this value rather than computing it from the last change: when a page is empty
/// the client still needs a cursor, and a server that skipped sequences — pruned records,
/// filtered rows — would otherwise make the client re-request the same gap forever.</para>
/// </param>
/// <param name="HasMore">
/// True when more changes are already waiting. A client draining a backlog should loop while
/// this is set instead of waiting for its next poll interval.
/// </param>
public sealed record PullResult(
    IReadOnlyList<RecordChange> Changes,
    long NextCursor,
    bool HasMore);
