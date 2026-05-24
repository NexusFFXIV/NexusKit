# Contributing to NexusKit

Thanks for considering a contribution. This doc covers the workflow; the release process lives in [RELEASING.md](RELEASING.md).

## Branch & PR workflow

All changes go through a Pull Request. Direct pushes to `main` are blocked by branch protection.

1. **Branch off `main`** with a descriptive name:
   ```powershell
   git checkout main; git pull
   git checkout -b <scope>/<short-summary>
   ```
   Suggested scopes: `feat/`, `fix/`, `chore/`, `docs/`, `refactor/`, `test/`. Example: `fix/persistence-cancel-token-leak`.

2. **Commit** with clear imperative messages. Scope prefix is optional but helpful:
   ```
   fix(Persistence): plumb caller's ct through factory create
   ```

3. **Push the branch and open a PR**:
   ```powershell
   git push -u origin <branch>
   gh pr create
   ```

4. **CI runs on the PR** — the `build` check must be green. Reviewers can request changes. Merge via squash once approved + green.

## Local development

The recommended dev layout is the **NexusFFXIV workspace** — clone all three NexusFFXIV repos into a common parent folder:

```
NexusFFXIV/
├── NexusFFXIV.sln              ← umbrella solution
├── Directory.Build.targets     ← swaps PackageRef→ProjectRef when sibling source exists
├── NexusKit/                   ← this repo
├── NexusKit.Modules/
└── PlayerNexusTracker/
```

The workspace's `Directory.Build.targets` automatically rewires `<PackageReference Include="NexusKit.*" />` to `<ProjectReference>` against the sibling source. Edits in this repo are picked up by consumer repos (`NexusKit.Modules`, `PlayerNexusTracker`) **immediately** — no NuGet publish round-trip.

Smoke-test before opening a PR:
```powershell
dotnet build NexusKit.sln -c Release
```

## Cutting a testing build

For changes that need real-world validation before going to all consumers (risky refactor, breaking API change, new feature), publish a **testing release** first instead of jumping straight to a stable tag:

1. Land the work on `main` via the normal PR flow.
2. From `main`, tag with a pre-release suffix:
   ```powershell
   git tag -a v0.2.0-rc.1 -m "v0.2.0-rc.1 — testing build for <reason>"
   git push origin v0.2.0-rc.1
   ```
3. CI publishes pre-release NuGets (NuGet treats `0.2.0-rc.1` as lower priority — consumers must explicitly opt in via `--prerelease` to pick them up). The GitHub Release is automatically marked **Pre-release** because the tag contains `-`.
4. After validation, cut the stable version (`v0.2.0`) — no separate code change, just the tag.

Suffix conventions (descending stability):
- `-rc.N` — release candidate (feature-complete, last sanity check)
- `-beta.N` — feature-complete but UI/edge-cases pending
- `-preview.N` — early feedback, may break

The PR that prepares the change does **not** need to know whether it will ship as testing or stable — that's a tag-time decision. The same PR can be tested as `-rc.1`, then promoted to stable later with no rebase or extra commit.

## Code style

See [docs/coding-conventions.md](docs/coding-conventions.md). Highlights that catch reviewers' attention:

- `m`-prefix on private instance fields (`mHttpFactory`, `mLog`)
- `ConfigureAwait(false)` on every background-I/O `await`
- File-scoped namespaces, one public type per file
- `Nullable` enabled — never `!` to silence the compiler unless proven non-null
- DI: singletons by default; no `IServiceProvider.GetService<T>()` inside method bodies

## License

By contributing, you agree your contribution is licensed under **AGPL-3.0-only** — the same license as this project. Derivative works and redistribution must remain open under AGPL.
