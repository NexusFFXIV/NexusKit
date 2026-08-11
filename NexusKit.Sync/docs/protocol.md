# Protocol (NexusKit.Sync)

The four operations, the envelopes they carry, and the guarantees behind them.

## One interface

```csharp
public interface ISyncProtocol
{
    Task<HandshakeResult>    HandshakeAsync(HandshakeRequest r, CancellationToken ct);
    Task<PushResult>         PushAsync(PushRequest r, CancellationToken ct);
    Task<PullResult>         PullAsync(PullRequest r, CancellationToken ct);
    Task<ContractDescriptor> DescribeAsync(ContractRef r, CancellationToken ct);
}
```

Four operations, and no fifth. Keeping the surface this small is what makes the norm
enforceable: a conformance suite can cover all of it, an in-process implementation can stand in
for a whole server during tests, and a second transport can be added without a plugin author
noticing.

Implementations must throw `SyncProtocolException` for anything the peer *reported*, and let
genuine transport faults surface as themselves. The caller decides what to retry, and it cannot
decide well if a dropped TCP connection and a rejected API key look alike.

## Routes

Built by `SyncRoutes`, in the shared assembly, so client and server derive them from the same
code rather than from two string literals that agree until one is edited.

```
POST v1/handshake
POST v1/{contract}/{collection}/push
GET  v1/{contract}/{collection}/pull?since=N&limit=M
GET  v1/contracts
GET  v1/contracts/{contract}?version=1.0
GET  health
GET  ready
```

## Handshake

The client states which contract it speaks, at which version, with which hash, and which
protocol version it implements. The server answers with the **negotiated** contract version,
the scopes this API key actually carries, and the batch limits to size requests against.

Granted scopes may be a subset of what the contract implies. A client should treat a missing
scope as a disabled feature rather than an error to retry — the user simply did not grant it.

`HandshakeResult.SessionToken` is optional and this stack's REST client ignores it. Caching one
would buy a little bandwidth and cost invalidation logic; the protocol is written so a client
that ignores it stays correct.

## Push

A batch of `RecordWrite`, each an upsert or a tombstone, each carrying an `OpId`.

**The `OpId` is what makes push idempotent.** When a response is lost and the client retries,
the server recognises the id and does not apply the write twice. A ULID is the intended shape —
sortable, so a server can prune its dedupe window by age rather than keeping every id ever seen.

`PushResult` returns **one outcome per record**, not one verdict per batch:

| Status | Meaning | Client action |
|---|---|---|
| `Accepted` | Stored | Drop the outbox entry |
| `Duplicate` | Already applied under this `OpId` | Drop it — the data is there |
| `Rejected` | Refused; problems attached | Do not retry unchanged; quarantine or discard |

Per-record outcomes exist because one malformed record in a batch of fifty should not force the
other forty-nine to be resent — and a client that cannot tell *which* record failed has no
option but to retry the whole batch forever.

Outcomes arrive in no guaranteed order. Match them by `OpId` rather than by position, so a
server free to reorder or coalesce internally stays interoperable.

A batch exceeding the advertised limit is rejected **whole** rather than truncated. A partially
applied batch the client believes was applied entirely is the worse failure.

## Pull

Cursor-based. The client sends the highest sequence it already holds; the server returns
changes strictly after it, plus a `NextCursor` and a `HasMore` flag.

**A cursor, not a timestamp.** Clocks disagree, and a client whose clock is two seconds fast
would silently skip every record written in those two seconds. A server-assigned monotonic
sequence has no such failure mode.

Take `NextCursor` from the response rather than computing it from the last change: an empty
page still needs a cursor, and a server that skipped sequences — pruned records, filtered rows
— would otherwise leave the client re-requesting the same gap forever.

Tombstones are carried explicitly, as `RecordChange` with `Deleted = true`. Omitting deleted
records would leave a client mirroring the collection with no way to learn that something it
already holds is gone.

## Describe

Fetches a registered contract document, **without authentication**. A contract declares shapes
and constraints, not data, and being able to read one before holding a key is what lets an
author check compatibility, generate code, or simply see what a server speaks.

## Errors

RFC 9457 Problem Details. Branch on `SyncProblem.Type` — the constants in `SyncProblemType` are
part of the wire surface, and renaming one is a breaking change because every peer that handles
a case by comparing against the old string silently stops handling it.

`Detail` is free text for humans. Never parse it.

Type-specific fields arrive in `Extensions`: a contract mismatch carries the versions the server
knows, a validation failure carries the per-field problems. That is what lets a client react
usefully instead of only reporting that something went wrong.

`SyncProtocolException.IsTransient` marks the cases where retrying the identical request could
plausibly succeed later — a rate limit that refills, or a 502/503/504 from something in front
of the server.

## Payload ownership

`RecordWrite.Payload` and `RecordChange.Payload` are `JsonElement`, borrowed from a
`JsonDocument` the caller owns. An implementation that retains one past the call must
`Clone()` it.
