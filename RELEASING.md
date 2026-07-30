# Releasing NexusKit

NexusKit follows [Semantic Versioning](https://semver.org/). Versions are derived from git tags via [MinVer](https://github.com/adamralph/minver) — at pack time, MinVer reads the nearest reachable `v*.*.*` tag and stamps the package version from it.

All 7 packages in this repo ship with the **same version** (synchronous release strategy). That fits the framework's tight internal coupling — there is no scenario where, say, `NexusKit.Core` 0.2.0 is meant to be consumed with `NexusKit.Hosting` 0.1.0.

> **Never add a `<Version>` to a csproj here.** MinVer deriving the version from the tag is what makes tag and package version impossible to desynchronise. PlayerNexusTracker had a hardcoded version instead and shipped a release whose assembly version did not match its tag, which made the release invisible to existing users. If you find yourself wanting to pin a version, that is a signal something else is wrong.

## Cutting a release

1. **Verify `main` is green**:
   ```powershell
   git fetch origin
   git checkout main; git pull
   gh run list --limit 1
   ```

2. **Pick the version**. Inspect the latest tag and increment per SemVer:
   ```powershell
   git describe --tags --abbrev=0
   ```
   - `vX.Y.(Z+1)` — patch: backwards-compatible bugfix
   - `vX.(Y+1).0` — minor: backwards-compatible feature
   - `v(X+1).0.0` — major: breaking change

3. **Tag with annotation**. The annotation's first line becomes the Release title:
   ```powershell
   git tag -a v0.2.0 -m "v0.2.0 — adds X, fixes Y"
   git push origin v0.2.0
   ```

4. **CI auto-publishes** `.github/workflows/ci.yml`:
   - Build in Release config
   - `dotnet pack` each project (7 `.nupkg` + 7 `.snupkg`)
   - Push to GitHub Packages at `https://nuget.pkg.github.com/NexusFFXIV/`
   - Create a GitHub Release with auto-generated notes (PRs since the previous tag) and attach all `.nupkg`/`.snupkg` files

5. **Verify**:
   - Release page: `https://github.com/NexusFFXIV/NexusKit/releases/tag/v0.2.0`
   - Packages: `https://github.com/orgs/NexusFFXIV/packages?repo_name=NexusKit`
   - New packages should be **public**. If they appear as private, change visibility per package (Package settings → Danger zone → Change visibility).

## Cross-repo coordination

A NexusKit release that breaks public API forces a cascade:

1. **NexusKit**: land changes + tag `vX.Y.Z` → 7 NuGets publish
2. **NexusKit.Modules**: pull `main`, adapt to the breaking change, land via PR, tag `vX.Y.Z` → 6 NuGets publish
3. **PlayerNexusTracker**: pull, adapt, land, tag `vX.Y.Z` → Plugin zip released

During local development the workspace `Directory.Build.targets` lets you edit all three repos against source — only the tag step needs to be sequential.

## Hotfix releases

When `main` has unreleased work but a bugfix is needed on top of the released tag:

```powershell
# Branch off the released tag
git checkout -b hotfix/<thing> v0.2.0

# Fix + commit + push, open PR against `main` for the long-term fix
# THEN tag the hotfix off the hotfix branch
git tag -a v0.2.1 -m "v0.2.1 — hotfix: <description>"
git push origin v0.2.1
```

MinVer derives the version from the tag's commit, so the hotfix tag is enough — it doesn't need to be on `main` first. Make sure the same fix gets merged into `main` separately so v0.3.0 doesn't regress.

## Pre-release versions (testing builds)

Unreleased commits on `main` build as `0.2.1-preview.0.N` where N is the commit count since `v0.2.0` (MinVer auto-suffix). To **publish** a pre-release deliberately, tag with an explicit suffix containing `-`:

```powershell
git tag -a v0.2.0-rc.1 -m "v0.2.0-rc.1"
git push origin v0.2.0-rc.1
```

What CI does with a pre-release tag — different from a stable tag in exactly two ways:

| Step | Stable tag (`v0.2.0`) | Pre-release tag (`v0.2.0-rc.1`) |
|---|---|---|
| NuGet version | `0.2.0` | `0.2.0-rc.1` (NuGet treats as lower priority — consumers must explicitly opt in via `--prerelease` or pin the version) |
| GitHub Release flag | normal release | **Pre-release** (set automatically because the tag contains `-`) |
| Everything else | identical | identical — same build, same packages, same release notes mechanism |

Suffix conventions (descending stability):
- `-rc.N` — release candidate (feature-complete, last sanity check)
- `-beta.N` — feature-complete but UI/edge-cases pending
- `-preview.N` — early feedback, may break

Consumers of NexusKit (NexusKit.Modules, plugins) **keep their existing `[X.Y.Z,)` `PackageReference` ranges** during pre-release periods — `PackageReference` ranges ignore pre-releases by default, so stable consumers continue to resolve the previous stable. A consumer that wants to test against the pre-release pins it explicitly:

```xml
<PackageReference Include="NexusKit.Core" Version="0.2.0-rc.1" />
```

After validation, cut the stable version (`v0.2.0`). **The consumers' constraint floor has to move forward** before they'll actually resolve `0.2.0`: NuGet's `PackageReference` picks the **lowest** version satisfying the range, not the latest, so leaving the floor at `[0.1.0,)` would keep resolving `0.1.0` even with `0.2.0` on the feed. The promotion step is:

1. Bump the range floor in each consumer's csprojs, e.g. `[0.1.0,)` → `[0.2.0,)`
2. Regenerate `packages.lock.json` (`dotnet restore --force-evaluate`, or rely on CI to refresh on the next push)
3. Tag the consumer

CI's "List outdated NuGet dependencies" step in each consumer flags this — if `Resolved < Latest` after a NexusKit release, the floor on that consumer still needs raising.
