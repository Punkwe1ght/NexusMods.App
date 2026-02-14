# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NexusMods.App is a cross-platform mod manager for PC games, built with .NET 9 and Avalonia UI. Supported games: Stardew Valley, Skyrim SE, Fallout 4, Fallout: New Vegas, Cyberpunk 2077, Baldur's Gate 3, and Mount & Blade II: Bannerlord.

## Build and Run Commands

```bash
# Restore (required after clone/submodule update)
git submodule update --init --recursive
dotnet restore

# Build
dotnet build

# Run the app
dotnet run --project src/NexusMods.App/NexusMods.App.csproj

# Run all tests (excluding networking/flakey)
dotnet test --filter "RequiresNetworking!=True&FlakeyTest!=True"

# Run a single test project
dotnet test tests/Games/NexusMods.Games.CreationEngine.Tests

# Run a specific test
dotnet test --filter "FullyQualifiedName~MyTestClass.MyTestMethod"

# Networking tests (requires API key)
NEXUS_API_KEY="<your-key>" dotnet test --filter "RequiresNetworking=True"

# Benchmarks
dotnet run --project benchmarks/NexusMods.Benchmarks/NexusMods.Benchmarks.csproj -c Release
```

## Architecture Overview

### Project Organization

- **`src/NexusMods.Abstractions.*`** — Interfaces and contracts only
- **`src/NexusMods.Games.*`** — Per-game plugins (installers, diagnostics, sort order, synchronizers)
- **`src/NexusMods.Networking.*`** — Store integrations (Steam, GOG, Epic) and web API clients
- **`src/NexusMods.DataModel`** — Persistence layer over MnemonicDB (immutable append-only DB on RocksDB)
- **`src/NexusMods.DataModel.SchemaVersions`** — Database migration system
- **`src/NexusMods.App`** — Main entry point (GUI and CLI in one binary)
- **`src/NexusMods.App.UI`** — Avalonia views and ReactiveUI view models
- **`src/NexusMods.Library`** — Library management for downloaded and added mods
- **`src/NexusMods.Collections`** — Mod collection support
- **`src/NexusMods.Sdk`** — Shared SDK types including the Loadout model and settings interfaces
- **`src/NexusMods.App.Generators.Diagnostics`** — Source generator for diagnostic templates

All NuGet versions live in `Directory.Packages.props` at the repo root. Individual `.csproj` files specify `<PackageReference>` without version attributes.

### Key Patterns

**Service Registration:** Every project exposes a `Services.cs` with `AddXxx(this IServiceCollection)` extension methods. `src/NexusMods.App/Services.cs` composes the top-level registrations via `AddApp()`.

**Game Plugin System:** Games implement `IGame` (from `NexusMods.Abstractions.Games`) and register via `services.AddGame<TGame>()`. Each game declares `LibraryItemInstallers[]`, `DiagnosticEmitters[]`, `Synchronizer`, and `SortOrderManager`.

**MnemonicDB Data Model:** Define entities as partial classes implementing `IModelDefinition` with static attribute fields. The `NexusMods.MnemonicDB.SourceGenerator` generates `ReadOnly` and `New` structs from these definitions. Mutate through transactions:
```csharp
using var tx = connection.BeginTransaction();
var loadout = new Loadout.New(tx) { Name = "My Loadout", ... };
var result = await tx.Commit();
```

**Mod Installation Pipeline:** A `LibraryItem` flows through a selected `ILibraryItemInstaller`, which creates a `LoadoutItemGroup` within a transaction and commits it to MnemonicDB.

**Diagnostics System:** Define templates in a `static partial class Diagnostics` using `DiagnosticTemplateBuilder` with the `[DiagnosticTemplate]` attribute — the `NexusMods.App.Generators.Diagnostics` source generator emits `Create*` factory methods on that class. Emitters implement `ILoadoutDiagnosticEmitter` and yield `Diagnostic` instances from the generated factories.

**Schema Migrations:** `NexusMods.DataModel.SchemaVersions` manages MnemonicDB schema evolution. Migrations implement `IMigration` (or `IScanningMigration` for full-DB scans), follow the naming convention `_NNNN_Description`, and register via `AddMigration<T>()` in `Services.cs`.

**Settings System:** Settings classes implement `ISettings` (from `NexusMods.Sdk.Settings`) with a static `Configure(ISettingsBuilder)` method. Register via `services.AddSettings<T>()`. The `ISettingsManager` handles persistence and retrieval.

**UI Layer:** Avalonia + ReactiveUI + R3. ViewModels extend `AViewModel<TInterface>`. The workspace system manages panels and navigation. DI-based view resolution uses `InjectedViewLocator`.

**Single Process Architecture:** One main process hosts the app. Additional CLI invocations connect via IPC (`CliClient`/`CliServer`) to the running process.

### Test Infrastructure

- **Framework:** xUnit with `Xunit.DependencyInjection` for constructor-injected services
- **Assertions:** FluentAssertions
- **Mocking:** NSubstitute
- **Fixtures:** AutoFixture
- **Snapshots:** Verify.Xunit
- **Scaffolding:** Each test project has a `Startup.cs` calling `AddDefaultServicesForTesting()` with game-specific registrations; `NexusMods.Games.TestFramework` provides base classes (`AGameTest<TGame>`, `ALoadoutDiagnosticEmitterTest<TGame>`); `NexusMods.StandardGameLocators.TestHelpers` provides `AddUniversalGameLocator<TGame>()` for faking game installations
- **Test traits:** `RequiresNetworking`, `FlakeyTest`, `RequiresApiKey` — the default filter excludes the first two

### Environment Variables

- **`NEXUS_API_KEY`** — Nexus Mods API key for networking and API-key tests
- **`GITHUB_TOKEN`** — GitHub API token (used by `NexusMods.Networking.GitHub`)

### Adding a New Game

1. Create `NexusMods.Games.YourGame` project in `src/`
2. Implement `IGame` with game metadata, store IDs, installers, and diagnostics
3. Add `Services.cs` with `AddYourGame(this IServiceCollection)` calling `services.AddGame<YourGame>()`
4. Register in `src/NexusMods.App/Services.cs` via `AddSupportedGames()`
5. Create matching test project in `tests/Games/`

### Enforced Roslyn Analyzer Errors

The `.globalconfig` enforces these as build errors:

- **CS4014** — Unawaited async call
- **CS8509** — Incomplete switch on enum
- **CA1069** — Duplicate enum values
- **CA2211** — Non-constant public static field
- **CA2021** — Incompatible Enumerable cast
