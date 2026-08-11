# NexusKit.Sync

The norm a plugin and its server agree on. Defines what a data *contract* is, how it is
serialised so both sides compute the same hash, and which operations exist on the wire.

**No Dalamud reference, and no package references at all.** A Dalamud plugin takes this
assembly on one side and an ASP.NET Core server on the other; anything it dragged along would
become somebody's version conflict. `System.Text.Json` is in-box on `net10`, which is all the
serialisation this needs.

This assembly contains no transport and no storage. The REST client lives in
`NexusKit.Modules.Sync`, the server in [NexusSyncServer](https://github.com/NexusFFXIV/NexusSyncServer).

## The idea in one page

A **contract** declares named datasets — *collections* — and their direction:

- **Uplink** — client to server. What a plugin collects while the game runs.
- **Downlink** — server to client. What an author curates on the web, which the game cannot
  provide.

The two are independent: a plugin may have three uplinks and one downlink, and nothing has to
correspond between them. See [docs/contracts.md](docs/contracts.md).

The contract is **data, not code**. A server registers the JSON document at runtime and
provisions storage, endpoints and scopes from it — no rebuild, no server code. The fluent
builder below emits exactly that document and exists only to give the client type safety.

## Public API

### Contract model — `Contracts/`

| Type | File | Purpose |
|---|---|---|
| `SyncContract` | `Contracts/SyncContract.cs` | A validated contract. Only constructible via `Create` (which validates the whole document) or `Define` (the fluent builder). Exposes `CanonicalJson`, `Hash` and `Scopes`, all computed lazily. |
| `CollectionDefinition` | `Contracts/CollectionDefinition.cs` | One named dataset: name, direction, key, fields, declared indexes, rate limit, retention. |
| `FieldDefinition` | `Contracts/FieldDefinition.cs` | One field: type, required, `Min`/`Max`, `MaxLength`. Constraints are enforced **server-side**; a client may check them too but is never the authority. |
| `SyncDirection` | `Contracts/SyncDirection.cs` | `Uplink` / `Downlink`. Deliberately no bidirectional value — see [docs/contracts.md](docs/contracts.md). |
| `FieldType` | `Contracts/FieldType.cs` | `String`, `Integer`, `Number`, `Boolean`, `Timestamp`, `Guid`. Each maps onto exactly one JSON representation. |
| `ContractVersion` | `Contracts/ContractVersion.cs` | `major.minor` with parsing, ordering and `IsCompatibleWith`. No patch component — a contract has no implementation to fix. |
| `RateLimitPolicy` | `Contracts/RateLimitPolicy.cs` | Per-collection write budget, counted on records rather than requests so batching cannot slip past it. |
| `ContractJson` | `Contracts/ContractJson.cs` | `Write` / `Parse` / `ComputeHash`. The canonical form — see [docs/canonical-form.md](docs/canonical-form.md). |
| `PayloadValidator` | `Contracts/PayloadValidator.cs` | Validates one record against a collection. Returns a `ValidationResult`; bad data is a runtime condition, not an exception. |
| `ValidationResult`, `ValidationProblem` | `Contracts/ValidationResult.cs` | Every problem found, not just the first. |
| `ContractCompatibility`, `CompatibilityResult` | `Contracts/ContractCompatibility.cs` | Whether a candidate version may replace a registered one — see [docs/versioning.md](docs/versioning.md). |
| `ContractScopes` | `Contracts/ContractScopes.cs` | Derives `reports:push` / `items:pull` from the contract. Scopes are computed, never hand-maintained. |
| `ContractNames` | `Contracts/ContractNames.cs` | Identifier rules for contract ids, collection and field names. |
| `DurationText` | `Contracts/DurationText.cs` | The `180d` / `12h` / `30m` / `45s` wire format. Parsing is lenient, formatting is canonical. |
| `ContractDefinitionException` | `Contracts/ContractDefinitionException.cs` | A structurally invalid contract *document*. Carries every problem at once. |

### Fluent builder — `Contracts/Building/`

| Type | File | Purpose |
|---|---|---|
| `SyncContractBuilder` | `Building/SyncContractBuilder.cs` | `Uplink<T>` / `Downlink<T>` / `Build`. Entry point is `SyncContract.Define(id, version)`. |
| `CollectionBuilder<T>` | `Building/CollectionBuilder.cs` | Infers fields from a POCO by reflection; `Key`, `Field`, `Ignore`, `Indexed`, `RateLimit`, `Retention`, `Live`. |
| `FieldBuilder` | `Building/FieldBuilder.cs` | `As`, `Required`, `Optional`, `Range`, `Min`, `Max`, `MaxLength`. |
| `SyncIgnoreAttribute` | `Building/SyncIgnoreAttribute.cs` | Excludes a property from inference. Affects the builder only — a hand-written document is its own authority. |

### Protocol — `Protocol/`

| Type | File | Purpose |
|---|---|---|
| `ISyncProtocol` | `Protocol/ISyncProtocol.cs` | The four operations both sides program against: `HandshakeAsync`, `PushAsync`, `PullAsync`, `DescribeAsync`. See [docs/protocol.md](docs/protocol.md). |
| `HandshakeRequest`, `HandshakeResult` | `Protocol/` | Version negotiation, granted scopes, advertised limits, optional server notice. |
| `PushRequest`, `PushResult` | `Protocol/` | A batch of writes and **one outcome per record** — a single bad record must not force the rest to be resent. |
| `RecordWrite`, `RecordOutcome`, `RecordWriteStatus` | `Protocol/` | Upsert or tombstone, carrying the `OpId` that makes a retry idempotent. |
| `PullRequest`, `PullResult`, `RecordChange` | `Protocol/` | Cursor-based paging with explicit tombstones. |
| `ContractRef`, `ContractDescriptor` | `Protocol/` | Identifying and publishing a contract. `DescribeAsync` needs no authentication — a document describes shapes, not data. |
| `SyncRoutes` | `Protocol/SyncRoutes.cs` | Every URL path, derived once so client and server cannot drift. |
| `SyncJson` | `Protocol/SyncJson.cs` | The shared serialiser settings for envelopes. Governs the envelope, never the records inside it. |
| `SyncProblem`, `SyncProblemType`, `SyncProtocolException` | `Protocol/` | RFC 9457 Problem Details, with stable `type` identifiers to branch on. |
| `SyncLimits` | `Protocol/SyncLimits.cs` | Batch and payload bounds a client sizes its requests against. |
| `SyncProtocolVersion` | `Protocol/SyncProtocolVersion.cs` | The **wire** version — distinct from the package version and from any contract's version. |
| `ApiKeyFormat` | `Protocol/ApiKeyFormat.cs` | The `nxs_` key shape, plus `Redact` for anywhere a key might be written down. |
| `ContractVersionJsonConverter` | `Protocol/` | Carries a version as `"1.0"` so there is one spelling of it everywhere. |

## Registration

Nothing to register. This assembly is types and rules — no services, no DI extension, no
background work. Consumers register an *implementation*:

```csharp
// plugin side
services.AddNexusKitSync("acme.myplugin", o => { … });   // NexusKit.Modules.Sync
```

## Defining a contract

```csharp
public static readonly SyncContract Contract = SyncContract
    .Define("acme.myplugin", "1.0")
    .Uplink<Report>("reports", c => c
        .Key(x => x.ItemId)
        .Field(x => x.Rating, f => f.Range(1, 5))
        .Indexed(x => x.ItemId)
        .RateLimit(perMinute: 60)
        .Retention(TimeSpan.FromDays(180)))
    .Downlink<Item>("items", c => c.Key(x => x.Id))
    .Build();
```

`Build()` validates and produces the canonical document; `Contract.CanonicalJson` is what you
upload to a server, `Contract.Hash` is what the handshake carries.

## Further reading

| Document | What it covers |
|---|---|
| [docs/contracts.md](docs/contracts.md) | Collections, directions, fields, keys, and why uplink and downlink are separate datasets |
| [docs/canonical-form.md](docs/canonical-form.md) | The serialisation rules, why each exists, and what breaks without them |
| [docs/protocol.md](docs/protocol.md) | The four operations, the envelopes, idempotency, cursors and tombstones |
| [docs/versioning.md](docs/versioning.md) | Three different version numbers, negotiation, and the compatibility rules |
