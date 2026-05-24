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

## Code style

See [docs/coding-conventions.md](docs/coding-conventions.md). Highlights that catch reviewers' attention:

- `m`-prefix on private instance fields (`mHttpFactory`, `mLog`)
- `ConfigureAwait(false)` on every background-I/O `await`
- File-scoped namespaces, one public type per file
- `Nullable` enabled — never `!` to silence the compiler unless proven non-null
- DI: singletons by default; no `IServiceProvider.GetService<T>()` inside method bodies

## License

By contributing, you agree your contribution is licensed under **AGPL-3.0-only** — the same license as this project. Derivative works and redistribution must remain open under AGPL.
