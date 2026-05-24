# NexusKit

**Plugin-agnostic framework for FINAL FANTASY XIV Dalamud plugins.**

[![CI](https://github.com/NexusFFXIV/NexusKit/actions/workflows/ci.yml/badge.svg)](https://github.com/NexusFFXIV/NexusKit/actions/workflows/ci.yml)
[![CodeQL](https://github.com/NexusFFXIV/NexusKit/actions/workflows/codeql.yml/badge.svg)](https://github.com/NexusFFXIV/NexusKit/actions/workflows/codeql.yml)
[![Release](https://img.shields.io/github/v/release/NexusFFXIV/NexusKit?label=release&logo=github)](https://github.com/NexusFFXIV/NexusKit/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Dalamud API](https://img.shields.io/badge/Dalamud_API-15-9D5BFF)](https://github.com/goatcorp/Dalamud)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)

## Overview

NexusKit gives Dalamud plugin authors a sturdy, batteries-included foundation. The seven libraries handle the recurring plumbing every non-trivial plugin needs — host composition, persistence + migrations, ImGui windows + auto-settings, IPC publishing/consuming, Lumina-backed game-data lookups, and chat notifications — so plugin code can stay focused on features.

Two architectural layering rules keep the framework reusable beyond a single plugin:

1. **Dalamud-free vs. Dalamud-tied.** `Core`, `Persistence`, and `Hosting` are pure .NET libraries with no Dalamud reference. `Ui`, `Ipc`, `GameData`, and `ChatNotifications` are the Dalamud-tied bridges that wire the abstractions to Dalamud APIs.
2. **No service-locator.** All registrations are explicit, grep-able `services.AddNexusKitXxx()` extension methods.

Built on top of NexusKit: [**NexusKit.Modules**](https://github.com/NexusFFXIV/NexusKit.Modules) — reusable feature modules (player tracking, Lodestone/FFXIVCollect bridges, plugin-IPC adapters).

## Packages

All packages are published to [GitHub Packages](https://github.com/orgs/NexusFFXIV/packages) under the `NexusFFXIV` org.

| Package | What it provides |
|---|---|
| `NexusKit.Core` | Abstractions, plugin-lifetime state machine, localisation, browser-launcher contract, logging sink. No Dalamud reference. |
| `NexusKit.Persistence` | EF Core + SQLite host, `INexusDbContextFactory` (auto-links the caller's `ct` with `IPluginLifetime.Stopping`), per-module migrations, `DbMaintenanceService`. |
| `NexusKit.Hosting` | `PluginHostBuilder.BuildAsync` — composition root, DI registration, lifetime wiring. |
| `NexusKit.Ui` | Dalamud-tied: window manager, auto-settings UI, dialog helpers. |
| `NexusKit.Ipc` | Dalamud-tied: `IIpcRegistry`, naming-conventions, JSON-payload helpers for cross-plugin contracts. |
| `NexusKit.GameData` | Dalamud-tied: Lumina sheet helpers, encounter/content lookups, world resolution. |
| `NexusKit.ChatNotifications` | Dalamud-tied: pluggable chat-notification framework with its own auto-settings tab. |

## Install

GitHub Packages requires authentication even for public reads. Add a `nuget.config` next to your `.sln`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github-nexusffxiv" value="https://nuget.pkg.github.com/NexusFFXIV/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github-nexusffxiv>
      <add key="Username" value="<your-github-login>" />
      <add key="ClearTextPassword" value="%GITHUB_PACKAGES_PAT%" />
    </github-nexusffxiv>
  </packageSourceCredentials>
</configuration>
```

Create a [classic PAT](https://github.com/settings/tokens) with scope `read:packages`, store it as `GITHUB_PACKAGES_PAT` env var, then:

```powershell
dotnet add package NexusKit.Core
dotnet add package NexusKit.Hosting
# ... etc
```

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using NexusKit.Hosting;

var host = await new PluginHostBuilder()
    .ConfigureServices(services =>
    {
        services.AddNexusKitCore();
        services.AddNexusKitPersistence();
        services.AddNexusKitHosting();
        // ... your plugin's own services
    })
    .BuildAsync();
```

## Architecture

See [docs/architecture.md](docs/architecture.md) for the full project graph and the lifecycle diagram. Coding conventions live in [docs/coding-conventions.md](docs/coding-conventions.md); the IPC naming contract is in [docs/ipc-catalog.md](docs/ipc-catalog.md).

## Build from source

```powershell
git clone https://github.com/NexusFFXIV/NexusKit.git
cd NexusKit
dotnet build NexusKit.sln -c Release
```

The Dalamud-tied projects (`Ui`, `Ipc`, `GameData`, `ChatNotifications`) need a local Dalamud install. CI installs it from `https://goatcorp.github.io/dalamud-distrib/latest.zip`; locally, having XIVLauncher's dev hook at `%APPDATA%\XIVLauncher\addon\Hooks\dev\` is sufficient.

## Contributing

PRs welcome. By contributing, you agree your contribution is licensed under AGPL-3.0-only. Read [docs/coding-conventions.md](docs/coding-conventions.md) before opening a PR — it documents the naming, nullability, and `ConfigureAwait` rules the codebase relies on.

## License

[AGPL-3.0-only](LICENSE). Forks and derivative works must remain open under the same license.
