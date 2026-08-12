# Versioning (NexusKit.Sync)

Three different version numbers travel through this system. Conflating them causes real
breakage, so they are named separately and moved for different reasons.

## The three

| Version | Lives in | Moves when | Who has to care |
|---|---|---|---|
| **Package version** | the git tag, via MinVer | anything in NexusKit ships, including docs | consumers referencing the NuGet |
| **Protocol version** | `SyncProtocolVersion.Current` | the wire surface changes incompatibly | every client and server, including ones we did not write |
| **Contract version** | each author's own document | that author changes their schema | only that contract's peers |

Two consequences worth stating outright:

**A package release does not imply a protocol change.** Most releases do not touch the wire at
all. Worse, NexusKit publishes all its packages synchronously with one version, so
`NexusKit.Sync` gets a new number whenever `NexusKit.Ui` ships — and a breaking change *there*
bumps the major *here*, making it look as though the protocol broke when it did not. The
authority is `SyncProtocolVersion.Current`, not the package version. `RELEASING.md` says so too.

**A contract version is not ours to move.** It belongs to whoever wrote the contract.

## Contract negotiation

At handshake the server picks a version rather than accepting or rejecting one:

1. `contractId` must be identical
2. **major must match**
3. within that major, the highest mutually supported minor is selected
4. the hash goes to the audit log and the response — for diagnosis, not as a gate

A client asking for 1.4 against a server on 1.2 negotiates 1.2 and must not use anything newer.

Point 4 is the one people expect to be different. Matching on hash equality sounds rigorous and
is unusable in practice: every trivial edit — a widened bound, a new optional field, a
reordered declaration — would lock out every deployed client at once. The model is HTTP content
negotiation and Protobuf, not a checksum.

## Compatibility checking

When a new version is registered alongside an existing one, `ContractCompatibility.Check`
decides whether it may replace it. Within a major, evolution must be **additive**.

| Change | Verdict | Why |
|---|---|---|
| Add an optional field | compatible | Nothing existing refers to it |
| Add a collection | compatible | Same |
| Widen a bound, raise a `MaxLength` | compatible | Everything valid yesterday is still valid |
| Change a type, **widening** it | compatible, noted | Every stored value is already a value of the new type |
| Change a type, **narrowing** it | compatible, noted | Possible for values that fit — the stored data decides, not the table |
| Change a type with **no conversion** | breaking | Stored data could not be reinterpreted at all |
| Add a **required** field | breaking | An older peer cannot send a field it has never heard of, so all its writes start failing |
| Remove a field or collection | breaking | Peers still using it stop working |
| Make an optional field required | breaking | Peers may legitimately omit it |
| **Tighten** a bound or `MaxLength` | breaking | Records valid yesterday are rejected today — from a user's seat, the feature broke |
| Change direction or key | breaking | Existing records become unreachable, or peers push at something that only reads |

Retention, rate limits and indexes are deliberately absent from that table. They change how the
server treats data, not what a peer may send or read, so tightening them is an operational
decision rather than a compatibility break.

### Type changes

`FieldTypeConversion` holds the relation, in two grades. A **widening** — `integer → number`,
anything `→ string` — cannot fail on any value, so it needs no knowledge of what is stored. A
**narrowing** — `number → integer`, `integer → boolean`, `string → guid` — is possible only for
values that fit, and the table deliberately declines to guess: `integer → boolean` is fine exactly
when the column holds nothing but 0 and 1, which is a fact about the data. The registry answers it
by asking the storage layer, and reports what it found.

The target's constraints are part of the verdict, not a separate check. `guid → string` is lossless
only if the string can hold 36 characters; capped shorter, it is a narrowing however the types read.
For the same reason, the bound comparisons in the table above apply only when the type is
**unchanged** — across a type change `FieldTypeConversion` already accounts for them, and it does so
knowing that a `guid` field's absent `MaxLength` means "not applicable" rather than "unbounded".

### Checking against every version, not just the newest

Conversions need not be transitive, so `ContractCompatibility.CheckAll` compares a candidate against
**every** minor still registered. `integer → string` widens and `string → guid` narrows, and both
pass on their own — but `integer → guid` is no conversion at all. A peer still on the first version,
which is allowed to stay there, would be handed GUIDs where it reads numbers, with every individual
registration having been approved. Comparing only against the highest registered minor cannot see
that.

A new **major** is not "incompatible" in the sense of being rejected — majors are exactly how
breaking changes ship. It simply does not *supersede* the registered one; both live side by
side, and clients migrate when they are rebuilt.

## The underlying reason

There is no moment at which every deployed client updates at once. A change that assumes there
is will break whoever updates last, and in a plugin ecosystem that is a user who has no idea a
protocol exists. Every rule above is a restatement of that.
