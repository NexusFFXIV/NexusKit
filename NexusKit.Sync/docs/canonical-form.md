# Canonical form (NexusKit.Sync)

How a contract is turned into bytes, and why every rule is there.

## The problem

Client and server each hold their own copy of a contract and each compute a hash over it. If
those hashes disagree for any reason other than a genuine difference in content, two peers
report a mismatch over a formatting accident — and the person debugging it sees "contract
mismatch" with no indication that both sides describe the same thing.

So `ContractJson.Write` removes every degree of freedom the serialiser would otherwise have.

## The rules

| Rule | Without it |
|---|---|
| No whitespace, no indentation | A pretty-printed copy hashes differently |
| Fixed property order, written explicitly rather than by reflection | Reordering a C# property changes the document |
| Collections sorted by name | Declaration order leaks into the hash |
| Field keys sorted ordinally | Same, per collection |
| `indexed` arrays sorted ordinally | Same, per list |
| Defaults omitted | "absent" and "explicitly false" become two spellings of one thing |
| Decimals normalised via `G29` | `1` and `1.0` are equal numbers but stringify apart |
| Durations formatted to the largest whole unit | `24h` and `1d` are the same duration |
| `InvariantCulture` throughout | A German build emits `1,5` and interoperates with nobody |

Sorting applies specifically to things that are **sets**: collections, field keys, index lists.
Their order carries no meaning, so it must not carry a hash either. Arrays whose order *is*
meaningful would not be sorted — there are none in the contract model today, and adding one
would need this document updated alongside it.

## The hash

SHA-256 over the UTF-8 bytes of the canonical document, lowercase hex.

It travels in the handshake, but **it is not a gate**. Matching on it would break every client
on any trivial edit, so version negotiation matches on `contractId` plus major version, and the
hash goes into the response and the audit log. That is where it earns its keep: when something
does go wrong, seeing both hashes in one line is the difference between a diff and a mystery.

## Parsing rejects unknown properties

`ContractJson.Parse` throws on any property it does not know.

This is the one place where being strict is the safer direction. Tolerating unknown properties
would mean an older server silently ignoring a constraint a newer author declared — enforcing
*less* than the contract says. A validation layer may fail closed; it must not fail open.

Property order in the input does not matter. Only the output of `Write` is canonical.

## Verifying it

`ContractJsonTests` in `localTools/tests/NexusKit.Sync.Tests` asserts:

- a golden document, byte for byte
- field and collection declaration order do not affect the output
- decimal scale does not affect the output
- the hash is stable and matches `ComputeHash` over the golden string
- parse round-trips, accepts any property order, and rejects unknown properties

**One gap worth knowing about:** these tests are not run by any CI. Serialisation bugs of this
class are invisible on the machine that introduces them — a culture-sensitive format or a
dictionary enumerated in hash order produces a build that agrees perfectly with itself and
mismatches against everybody else. Running the suite on a non-Windows machine occasionally is
the cheap mitigation; a second CI job over an `[ubuntu, windows]` matrix building only this
project is the real one, and is noted in that project's README.
