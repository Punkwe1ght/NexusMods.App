# Fallout: New Vegas — Gold Standard Integration Design

## Goal

Bring the FNV integration to feature parity with Stardew Valley. Cover the three most common FNV mod failure modes — xNVSE plugin mismanagement, INI conflicts, and orphaned BSA archives — with custom data models, a game-specific installer, diagnostics, settings, version detection, file hash integration, and comprehensive tests.

## Current State

FNV has a solid foundation: game class, synchronizer, `Tes4HeaderParser` (independent of Mutagen), `StopPatternInstaller` with FNV-specific patterns, xNVSE tool registration, and five diagnostic emitters (archive invalidation, 4GB patcher, plugin limits, mod limit fix, xNVSE detection). One installer test scaffold exists with two mod cases.

FNV lacks: custom data models, a game-specific installer, settings, `GetLocalVersion()`, `GetFallbackCollectionInstallDirectory()`, file hash integration, INI/BSA diagnostics, and comprehensive tests.

## Architecture

Three failure domains drive the design. Each maps to a model, installer logic, diagnostics, and tests:

| Failure Domain | Model | Installer Logic | New Diagnostics |
|---|---|---|---|
| xNVSE plugins | `NvsePluginLoadoutItem` | Detect `Data/NVSE/Plugins/*.dll`, tag with model | `NvseVersionMismatchEmitter` |
| INI management | `IniTweakLoadoutFile` | Detect `.ini` files targeting known FNV INIs, tag with model | `IniConflictEmitter` |
| BSA archives | None (opaque binaries) | Already handled by `StopPatternInstaller` | `BsaLoadOrderEmitter` |

Cross-cutting additions: `FalloutNVSettings`, `GetLocalVersion()`, `GetFallbackCollectionInstallDirectory()`, `IFileHashesService` integration, and 11 test classes.

## Custom Data Models

Two new models in `src/NexusMods.Games.CreationEngine/FalloutNV/Models/`.

### NvsePluginLoadoutItem

Extends `LoadoutItemGroup`. Marks mod groups that contain xNVSE plugins.

```csharp
[Include<LoadoutItemGroup>]
public partial class NvsePluginLoadoutItem : IModelDefinition
{
    private const string Namespace = "NexusMods.CreationEngine.FalloutNV.NvsePluginLoadoutItem";

    public static readonly MarkerAttribute NvsePlugin =
        new(Namespace, nameof(NvsePlugin)) { IsIndexed = true };

    public static readonly StringAttribute PluginVersion =
        new(Namespace, nameof(PluginVersion)) { IsOptional = true };
}
```

- `NvsePlugin` — indexed marker for querying all NVSE mod groups in a loadout
- `PluginVersion` — optional version string extracted from the DLL, used by `NvseVersionMismatchEmitter`

### IniTweakLoadoutFile

Extends `LoadoutFile`. Marks files that are INI fragments.

```csharp
[Include<LoadoutFile>]
public partial class IniTweakLoadoutFile : IModelDefinition
{
    private const string Namespace = "NexusMods.CreationEngine.FalloutNV.IniTweakLoadoutFile";

    public static readonly MarkerAttribute IniTweakFile =
        new(Namespace, nameof(IniTweakFile)) { IsIndexed = true };

    public static readonly StringAttribute TargetIniFile =
        new(Namespace, nameof(TargetIniFile));
}
```

- `IniTweakFile` — indexed marker for querying all INI tweaks in a loadout
- `TargetIniFile` — which INI the tweak targets (`Fallout.ini`, `FalloutPrefs.ini`, `FalloutCustom.ini`, `GECKCustom.ini`)

Register both in `Services.cs`:

```csharp
.AddNvsePluginLoadoutItemModel()
.AddIniTweakLoadoutFileModel()
```

## Settings

`FalloutNVSettings.cs` in `src/NexusMods.Games.CreationEngine/FalloutNV/`:

```csharp
public class FalloutNVSettings : ISettings
{
    public bool DoFullGameBackup { get; set; } = false;
    public bool CheckArchiveInvalidation { get; set; } = true;

    public static ISettingsBuilder Configure(ISettingsBuilder settingsBuilder)
    {
        return settingsBuilder
            .ConfigureProperty(x => x.DoFullGameBackup, new PropertyOptions<FalloutNVSettings, bool>
            {
                Section = Sections.Experimental,
                DisplayName = "Full game backup: Fallout New Vegas",
                DescriptionFactory = _ =>
                    "Backup all game folders. Increases disk usage. Change before managing the game.",
            }, new BooleanContainerOptions())
            .ConfigureProperty(x => x.CheckArchiveInvalidation, new PropertyOptions<FalloutNVSettings, bool>
            {
                Section = Sections.General,
                DisplayName = "Check archive invalidation: Fallout New Vegas",
                DescriptionFactory = _ =>
                    "Warn when archive invalidation is not enabled in INI files.",
            }, new BooleanContainerOptions());
    }
}
```

The `ArchiveInvalidationEmitter` reads `CheckArchiveInvalidation` via `ISettingsManager` and skips its check when disabled.

Register in `Services.cs`:

```csharp
.AddSettings<FalloutNVSettings>()
```

## Version Detection and Collection Support

Add two methods to `FalloutNV.cs`:

### GetLocalVersion

Reads `FalloutNV.exe` file version from the game directory.

```csharp
public Optional<Version> GetLocalVersion(GameInstallation installation)
{
    try
    {
        var exePath = installation.Locations[LocationId.Game].Path / "FalloutNV.exe";
        return exePath.FileInfo.GetFileVersionInfo().FileVersion;
    }
    catch (Exception)
    {
        return Optional<Version>.None;
    }
}
```

### GetFallbackCollectionInstallDirectory

Points collections to `Data/` when no installer-specific directory applies.

```csharp
public Optional<GamePath> GetFallbackCollectionInstallDirectory(GameInstallation installation)
{
    return Optional<GamePath>.Create(new GamePath(LocationId.Game, "Data"));
}
```

## File Hashes Integration

No special registration needed. `IFileHashesService` is globally available. The `FnvModInstaller` injects it for game file validation, matching the Stardew Valley pattern:

```csharp
public FnvModInstaller(
    IServiceProvider serviceProvider,
    ILogger<FnvModInstaller> logger,
    IFileHashesService fileHashesService,
    IFileStore fileStore)
    : base(serviceProvider, logger)
{
    _fileHashesService = fileHashesService;
    _fileStore = fileStore;
}
```

## Game-Specific Installer

`FnvModInstaller.cs` in `src/NexusMods.Games.CreationEngine/FalloutNV/Installers/`. Extends `ALibraryItemInstaller`.

### Behavior

1. **xNVSE plugin detection:** Archive contains files matching `Data/NVSE/Plugins/*.dll` — create `NvsePluginLoadoutItem` group wrapping the standard file entries.
2. **INI tweak detection:** Archive contains `.ini` files targeting known FNV INI filenames — create `IniTweakLoadoutFile` entities with `TargetIniFile` set.
3. **Fallthrough:** Archive contains neither pattern — return `NotSupported`. The next installer in the chain handles it.
4. **Mixed archives:** Archive contains xNVSE plugins AND regular data files AND INI tweaks — install everything, tag the relevant files.

### Installer Ordering

```csharp
LibraryItemInstallers =
[
    FomodXmlInstaller.Create(provider, new GamePath(LocationId.Game, "Data")),
    new FnvModInstaller(provider),
    new StopPatternInstaller(provider) { ... }.Build(),
];
```

FOMOD stays first (interactive UI flow). `FnvModInstaller` handles non-FOMOD archives with xNVSE or INI patterns. `StopPatternInstaller` catches everything else.

## New Diagnostics

Three new emitters in `src/NexusMods.Games.CreationEngine/FalloutNV/Emitters/`.

### IniConflictEmitter

**Trigger:** Two or more `IniTweakLoadoutFile` entries target the same INI file and set the same key to different values.

**Severity:** Warning.

**Implementation:** Query all `IniTweakLoadoutFile` entities in the loadout, group by `TargetIniFile`, parse each file's key-value pairs, and detect conflicts. Report which mods set conflicting values for which keys.

### BsaLoadOrderEmitter

**Trigger:** A `.bsa` file in `Data/` has no matching `.esp` or `.esm` with the same filename stem. FNV only loads BSA archives that match a loaded plugin name.

**Severity:** Warning.

**Implementation:** Scan the sync tree for `.bsa` files in `Data/`. For each BSA, check whether a plugin with the same stem exists. Report orphaned BSAs.

### NvseVersionMismatchEmitter

**Trigger:** xNVSE loader is present but an `NvsePluginLoadoutItem` has a `PluginVersion` requiring a newer xNVSE version than what the loader provides.

**Severity:** Warning.

**Implementation:** Read xNVSE loader version from file metadata. Compare against `PluginVersion` fields on all `NvsePluginLoadoutItem` entities. Report mismatches when version data is available; skip silently when version metadata is absent.

### Diagnostic Templates

Add to `FalloutNVDiagnostics.cs`:

```csharp
[DiagnosticTemplate]
internal static IDiagnosticTemplate IniConflict = DiagnosticTemplateBuilder
    .Start()
    .WithId(new DiagnosticId(Source, number: 7))
    .WithTitle("INI Setting Conflict")
    .WithSeverity(DiagnosticSeverity.Warning)
    .WithSummary("{ConflictCount} INI settings have conflicting values across mods")
    .WithDetails("...")
    .WithMessageData(messageBuilder => messageBuilder.AddValue<int>("ConflictCount"))
    .Finish();

[DiagnosticTemplate]
internal static IDiagnosticTemplate OrphanedBsa = DiagnosticTemplateBuilder
    .Start()
    .WithId(new DiagnosticId(Source, number: 8))
    .WithTitle("Orphaned BSA Archive")
    .WithSeverity(DiagnosticSeverity.Warning)
    .WithSummary("{BsaName} has no matching plugin — the game will not load it")
    .WithDetails("...")
    .WithMessageData(messageBuilder => messageBuilder.AddValue<string>("BsaName"))
    .Finish();

[DiagnosticTemplate]
internal static IDiagnosticTemplate NvseVersionMismatch = DiagnosticTemplateBuilder
    .Start()
    .WithId(new DiagnosticId(Source, number: 9))
    .WithTitle("xNVSE Version Too Old")
    .WithSeverity(DiagnosticSeverity.Warning)
    .WithSummary("Installed xNVSE is older than version required by {PluginName}")
    .WithDetails("...")
    .WithMessageData(messageBuilder => messageBuilder.AddValue<string>("PluginName"))
    .Finish();
```

Register all new emitters in `Services.cs` and add to `DiagnosticEmitters[]` in `FalloutNV.cs`.

## Test Suite

11 new test classes in `tests/Games/NexusMods.Games.CreationEngine.Tests/FalloutNV/`. The existing `Startup.cs` already registers FNV — no changes needed.

### Emitter Tests (8 classes)

Each extends `ALoadoutDiagnosticEmitterTest<TTest, FalloutNV, TEmitter>`:

| Test Class | Verifies |
|---|---|
| `ArchiveInvalidationEmitterTests` | Fires when INI lacks `bInvalidateOlderFiles=1`; silent when present; respects settings toggle |
| `FourGbPatcherEmitterTests` | Fires when `FalloutNV_backup.exe` missing; silent when present |
| `PluginLimitEmitterTests` | Warning at 130 plugins; critical at 255; silent below 130 |
| `ModLimitFixEmitterTests` | Fires when >139 plugins and no `mod_limit_fix.dll`; silent when present |
| `XnvseDetectionEmitterTests` | Fires when NVSE plugins exist but no `nvse_loader.exe`; silent when loader present |
| `IniConflictEmitterTests` | Fires when two INI tweaks set same key differently; silent when keys align |
| `BsaLoadOrderEmitterTests` | Fires when BSA has no matching plugin; silent when matched |
| `NvseVersionMismatchEmitterTests` | Fires when loader version < required; silent when satisfied or no version data |

### Installer Tests (1 class)

`FnvModInstallerTests` extends `ALibraryArchiveInstallerTests<TTest, FalloutNV>`:

| Test Case | Verifies |
|---|---|
| xNVSE mod (e.g., JIP LN NVSE) | Creates `NvsePluginLoadoutItem` with marker set |
| INI tweak mod | Creates `IniTweakLoadoutFile` with correct `TargetIniFile` |
| Mixed archive | Tags both xNVSE and INI files; installs data files normally |
| Data-only archive | Returns `NotSupported`; falls through to `StopPatternInstaller` |

Uses `[Trait("RequiresNetworking", "True")]` and `[Trait("RequiresApiKey", "True")]` for tests that download from Nexus.

### Synchronizer Test (1 class)

`FalloutNVSynchronizerTests`:

| Test Case | Verifies |
|---|---|
| Round-trip apply/ingest | Loadout survives apply then ingest without data loss |
| `plugins.txt` generation | ESM-before-ESP ordering; no ESL entries |
| BSA backup exclusion | `.bsa` files excluded from backup via `IsIgnoredBackupPath` |

### Test Trait Strategy

Emitter tests use synthetic loadouts — no network required. Installer tests that download real mods from Nexus use `RequiresNetworking` and `RequiresApiKey`. Synchronizer tests use synthetic loadouts.

## File Summary

### New Files (16)

| Path | Purpose |
|---|---|
| `src/.../FalloutNV/Models/NvsePluginLoadoutItem.cs` | xNVSE plugin data model |
| `src/.../FalloutNV/Models/IniTweakLoadoutFile.cs` | INI tweak data model |
| `src/.../FalloutNV/FalloutNVSettings.cs` | Game settings |
| `src/.../FalloutNV/Installers/FnvModInstaller.cs` | Game-specific installer |
| `src/.../FalloutNV/Emitters/IniConflictEmitter.cs` | INI conflict diagnostic |
| `src/.../FalloutNV/Emitters/BsaLoadOrderEmitter.cs` | Orphaned BSA diagnostic |
| `src/.../FalloutNV/Emitters/NvseVersionMismatchEmitter.cs` | xNVSE version diagnostic |
| `tests/.../FalloutNV/ArchiveInvalidationEmitterTests.cs` | Test |
| `tests/.../FalloutNV/FourGbPatcherEmitterTests.cs` | Test |
| `tests/.../FalloutNV/PluginLimitEmitterTests.cs` | Test |
| `tests/.../FalloutNV/ModLimitFixEmitterTests.cs` | Test |
| `tests/.../FalloutNV/XnvseDetectionEmitterTests.cs` | Test |
| `tests/.../FalloutNV/IniConflictEmitterTests.cs` | Test |
| `tests/.../FalloutNV/BsaLoadOrderEmitterTests.cs` | Test |
| `tests/.../FalloutNV/NvseVersionMismatchEmitterTests.cs` | Test |
| `tests/.../FalloutNV/FnvModInstallerTests.cs` | Test |

### Modified Files (4)

| Path | Change |
|---|---|
| `src/.../FalloutNV/FalloutNV.cs` | Add `GetLocalVersion()`, `GetFallbackCollectionInstallDirectory()`, update `LibraryItemInstallers[]` and `DiagnosticEmitters[]` |
| `src/.../FalloutNV/FalloutNVDiagnostics.cs` | Add 3 new diagnostic templates (IDs 7-9) |
| `src/.../Services.cs` | Register models, settings, new emitters, new installer |
| `src/.../FalloutNV/Emitters/ArchiveInvalidationEmitter.cs` | Read `CheckArchiveInvalidation` setting |

### Unchanged Files

All existing emitters, synchronizer, known paths, test startup, and existing installer tests remain unchanged.

## Implementation Order

1. Custom data models (no dependencies)
2. Settings (no dependencies)
3. `GetLocalVersion()` and `GetFallbackCollectionInstallDirectory()` (no dependencies)
4. `FnvModInstaller` (depends on models)
5. New diagnostic templates (no dependencies)
6. New emitters (depend on models and templates)
7. Wire everything in `Services.cs` and `FalloutNV.cs` (depends on all above)
8. Tests (depend on all above)
