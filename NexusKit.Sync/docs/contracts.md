# Contracts (NexusKit.Sync)

What a contract declares, and why it is shaped this way.

## The contract

A contract is an identity, a version, and a set of collections:

```jsonc
{
  "contractId": "acme.myplugin",
  "version": "1.0",
  "collections": [
    {
      "name": "reports",
      "direction": "uplink",
      "key": "item_id",
      "fields": {
        "item_id": { "type": "string", "required": true, "maxLength": 64 },
        "rating":   { "type": "integer", "min": 1, "max": 5 }
      },
      "indexed":   ["item_id"],
      "rateLimit": { "perMinute": 60 },
      "retention": "180d"
    }
  ]
}
```

That document is the authority. `SyncContract.Define(...)` is a convenience that emits exactly
it; a hand-written file parsed with `ContractJson.Parse` produces an equally valid contract.

The reason for that split is practical rather than aesthetic. If a contract were a compiled
library, a plugin author changing a field would have to publish a NuGet and the server operator
would have to rebuild an image. Because it is data, one published server image serves every
author, and registering a contract is a runtime operation.

## Collections

A **collection** is a named dataset — the contract equivalent of a table or a topic. Name,
schema, direction. Nothing else.

## Directions are separate datasets

| Direction | Flow | Who writes | Typical content |
|---|---|---|---|
| `uplink` | client → server | the plugin | what players encounter in-game |
| `downlink` | server → client | the author's web UI | curated data the game cannot provide |

The important part: **these are different collections, not two sides of one.** A plugin might
push three uplinks and mirror one downlink, and nothing has to correspond between them. The
common shape is exactly that — a plugin collects observations, and separately displays a
catalogue its author maintains outside the game.

There is deliberately no bidirectional direction. Allowing the same collection to be written
from both ends forces conflict resolution into *every* implementation — client, server, and any
third-party one — and the cases that genuinely need it are rare enough to model as two
collections plus an explicit reconciliation step.

## Keys

Every collection names one field as its key, and that field must be required and of type
`String`, `Integer` or `Guid`.

The key is what makes three separate things work:

- **Idempotency** — two writes with the same key are the same record, so a client retrying
  after a dropped response updates rather than duplicates.
- **Tombstones** — a delete has to name something, and by then the payload is gone.
- **Addressing** — the mirror on the client side is keyed by it.

A key that could be absent would break all three, which is why `Key(...)` on the builder forces
the field required rather than letting the author discover it as a validation error later.

## Fields

Six types, each mapping onto exactly one JSON representation, so a server can validate a
payload without knowing anything about the CLR types the author used.

Two of them are narrower than they look:

**`Timestamp`** requires an explicit UTC offset — a trailing `Z` or `±hh:mm`. A payload without
one is rejected rather than assumed. A client in Berlin and one in Tokyo both sending
`2026-08-04T12:00:00` mean instants nine hours apart, and whichever default the server picked
would be wrong for one of them.

**`ulong` has no `Integer` mapping.** Values above `long.MaxValue` have no JSON-number form that
every implementation reads back identically, so the builder maps `ulong` to `String`. FFXIV
ContentIds live in exactly that range, which is why it comes up.

`DateTime` is unsupported outright — its `Kind` survives neither JSON nor most databases, so
"the same instant" quietly stops being the same instant somewhere in the middle. Use
`DateTimeOffset`.

## Constraints

`Required`, `Min`/`Max`, `MaxLength`. All enforced **server-side on every write**.

That placement is the whole point. A client may validate too, for a faster and friendlier
error, but it is never the authority — the contract exists so that a forged, buggy or outdated
client cannot store something the contract forbids. Any change that makes validation more
permissive is a security change, not a convenience change.

## Indexes

Records are stored generically so contracts can be registered at runtime, which means there is
no migration in which anyone could hand-write an index. `indexed` is how a collection gets
targeted query performance anyway: the server creates an generated column indexed on the JSON path for
each listed field.

## Retention

Optional, in the `180d` / `12h` / `30m` / `45s` format. Declaring one is the cheapest privacy
control available — data that has been deleted cannot leak.

## Scopes come from the contract

Each collection implies exactly one scope, `reports:push` or `items:pull`, derived from its
name and direction. They are computed rather than declared separately, for two reasons:

- a hand-maintained scope list drifts from the collections it guards, and the failure mode of
  that drift is a scope granting access to something nobody remembers approving
- the verb follows the direction, so a scope cannot express an operation the collection does
  not support — there is no way to *name* `items:push` for a downlink collection

## Naming rules

Contract ids are dot-separated lowercase segments with at least two parts (`acme.myplugin`).
The leading segment is the author's namespace; without it, two unrelated authors both calling
their contract `tracker` collide the moment their contracts meet on one server.

Collection and field names are lowercase letters, digits and underscores, starting with a
letter. Narrower than JSON allows, because these names also become storage paths, index names,
scope strings and URL segments — each with its own idea of what is legal.

The builder derives field names with `JsonNamingPolicy.SnakeCaseLower`, so `ItemId` becomes
`item_id`. The payload serialiser has to apply the same policy, and it does so by using the
very same instance rather than a reimplementation — two independent snake_case implementations
agreeing on `IOPort` is not something to leave to chance.
