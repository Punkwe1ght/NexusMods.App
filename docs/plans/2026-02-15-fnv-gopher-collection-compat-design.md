# FNV Gopher Collection Compatibility Design

**Date:** 2026-02-15
**Goal:** Make NexusMods.App fully compatible with Gopher's Fallout: New Vegas mod collections, validated by automated tests, deployable to Steam Deck via Proton.

**Target collections:**
- Gopher's Stable New Vegas (`60wuix`)
- Gopher's New Vegas Remaster (100+ mods)
- Gopher's DarNified UI
- Gopher's Gameplay and QoL Tweaks

## Gap Analysis

| Requirement | Current State | Gap | Severity |
|-------------|--------------|-----|----------|
| Plugin load ordering (.esp/.esm) | FNV registers zero sort order varieties | Collection `modRules` never translate to plugin.txt | High |
| FOMOD with predefined choices | Hardcodes `Data/` prefix on all destinations | FOMODs targeting root-level paths (NVSE/) get double-prefixed | High |
| Proton/Wine diagnostics | No FNV-specific Proton checks | xNVSE + 4GB patcher need Proton guidance | Medium |
| Replicated mods | MD5-based path mapping works correctly | None | - |
| xNVSE plugin detection | FnvModInstaller detects and tags | None | - |
| INI tweak handling | FnvModInstaller detects, IniConflictEmitter validates | None | - |
| BSA validation | BsaLoadOrderEmitter checks orphans | None | - |
| External tool downloads | Browse sources mark ManualOnly | No blocking UX, but functional | Low |

## Architecture

Four components, no new abstractions. All build on existing infrastructure.

```
                    Collection JSON
                         |
                    ParseCollectionJson
                         |
              +----------+----------+
              |          |          |
          modRules    mods[]    choices
              |          |          |
    [1] PluginSort  [2] FOMOD   Installer
        Variety      PathNorm    Pipeline
              |          |          |
              v          v          v
         plugin.txt   Correct    Loadout
         ordering     file        with
                     placement   diagnostics
                                     |
                              [3] ProtonReqs
                                  Emitter
                                     |
                              [4] Collection
                                  Parser Tests
```

## Component 1: FNV Plugin Sort Order Variety

**Files:**
- `src/NexusMods.Games.CreationEngine/FalloutNV/SortOrder/FnvPluginSortOrderVariety.cs` (new)
- `src/NexusMods.Games.CreationEngine/FalloutNV/FalloutNV.cs` (modify line 49)

**Design:**

FNV needs a sort order variety that tracks `.esp` and `.esm` files. The existing `SortOrderManager` framework handles persistence and UI; the variety defines what items participate and how to read/write ordering.

```csharp
public class FnvPluginSortOrderVariety : ISortOrderVariety
{
    // Identifies .esp/.esm files in the loadout
    // Reads priority from collection modRules (before/after constraints)
    // Writes ordered list to plugins.txt at LocationId.AppData
}
```

**Key behaviors:**
- Filters loadout items to `.esp` and `.esm` files only
- Respects collection `modRules` with type `before`/`after` as ordering constraints
- Topological sort resolves constraint graph into linear order
- Writes `plugins.txt` with one plugin per line, asterisk prefix for active plugins
- Master files (.esm) sort before plugins (.esp) within constraint groups
- Handles circular dependencies by logging a warning and falling back to alphabetical order

**Registration change in FalloutNV.cs:**
```csharp
// Before:
sortOrderManager.RegisterSortOrderVarieties([], this);

// After:
sortOrderManager.RegisterSortOrderVarieties(
    [new FnvPluginSortOrderVariety(provider)], this);
```

## Component 2: FOMOD Path Normalization

**Files:**
- `src/NexusMods.Games.FOMOD/FomodXmlInstaller.cs` (modify path resolution)

**Design:**

The `FomodXmlInstaller` creates `TargetPath` by joining the game's base install path (for FNV: `Data/`) with the FOMOD destination. Some FOMODs (DarNified UI) specify destinations that already include `Data/` or target paths outside `Data/` (like `NVSE/Plugins/`).

**Normalization rules:**
1. Strip leading `Data/` or `data/` from FOMOD destination if the game's base path already includes `Data/`
2. If a FOMOD destination starts with a known root-level directory (`NVSE/`, `nvse/`), install relative to game root instead of `Data/`
3. Log a warning when normalization triggers, identifying the FOMOD and the adjusted path

**Known root-level directories for FNV:**
- `NVSE/` — Script extender plugins
- `nvse/` — Case variant on Linux

**Implementation approach:**
Add a virtual method `NormalizeModInstallerPath(GamePath basePath, RelativePath fomodDest)` to the game interface or handle it in the FomodXmlInstaller's path construction. The installer checks if `fomodDest` starts with a known root directory and adjusts `LocationId` accordingly.

## Component 3: Proton/Wine Diagnostics for FNV

**Files:**
- `src/NexusMods.Games.CreationEngine/FalloutNV/Emitters/ProtonRequirementsEmitter.cs` (new)
- `src/NexusMods.Games.CreationEngine/FalloutNV/Diagnostics.cs` (add templates)
- `src/NexusMods.Games.CreationEngine/Services.cs` (register)

**Design:**

Follows the pattern established by Cyberpunk's `MissingProtontricksForRedModEmitter` and `WinePrefixRequirementsEmitter`.

**Diagnostic templates:**

| Template | Severity | Trigger |
|----------|----------|---------|
| `XnvseRequiresProtontricks` | Warning | xNVSE plugins in loadout + Linux + no protontricks available |
| `FourGbPatcherProtonWarning` | Warning | 4GB patcher backup detected + Linux (patching exe under Proton may cause issues) |
| `WineDllOverrideSuggestion` | Suggestion | xNVSE in loadout + Linux + `WINEDLLOVERRIDES` missing `nvse_loader=n` |

**Emitter logic:**
```
1. Skip if not running on Linux
2. Check if game installation uses Steam (Proton) or GOG (Heroic/Wine)
3. Scan loadout for xNVSE plugins (NvsePluginLoadoutItem marker)
4. If found:
   a. Check protontricks availability via IAggregateProtontricksDependency
   b. Parse WINEDLLOVERRIDES from Wine prefix config
   c. Emit diagnostics for missing requirements
5. Scan for 4GB patcher backup (FalloutNV_backup.exe)
   a. If found on Linux, emit compatibility warning
```

## Component 4: Collection JSON Parser Tests

**Files:**
- `tests/Games/NexusMods.Games.CreationEngine.Tests/FalloutNV/Collections/GopherCollectionParsingTests.cs` (new)
- `tests/Games/NexusMods.Games.CreationEngine.Tests/FalloutNV/Collections/Fixtures/` (new directory)
  - `gopher-stable-nv.json`
  - `gopher-remaster.json`
  - `gopher-darnified-ui.json`
  - `gopher-qol-tweaks.json`

**Design:**

Each fixture models the structure of a real Gopher collection using the `CollectionRoot` JSON schema. Fixtures contain representative mods covering every source type and installation path.

### Fixture: gopher-stable-nv.json

Models Gopher's base stability collection. Contains:
- **xNVSE** (browse source, external URL to silverlock.org)
- **4GB Patcher** (browse source, external URL)
- **Yukichigai Unofficial Patch** (NexusMods source, .esp plugin)
- **NVAC - New Vegas Anti Crash** (NexusMods source, NVSE plugin DLL)
- **NVTF - New Vegas Tick Fix** (NexusMods source, NVSE plugin DLL + INI config)
- **JIP LN NVSE Plugin** (NexusMods source, NVSE plugin DLL)
- **Johnny Guitar NVSE** (NexusMods source, NVSE plugin DLL)
- **modRules**: after constraints for plugin load order

### Fixture: gopher-remaster.json

Models Gopher's 100+ mod visual overhaul. Contains:
- **NMC Texture Pack** (NexusMods source, BSA archive)
- **Ojo Bueno** (NexusMods source, BSA archive + .esp)
- **Interior Lighting Overhaul** (NexusMods source, FOMOD with predefined choices)
- **EVE** (NexusMods source, FOMOD with predefined choices)
- **Weapon Retexture Project** (NexusMods source, BSA archive)
- **Character Overhaul** (NexusMods source, .esp + textures)
- **Bundled compatibility patches** (bundle source)
- **modRules**: complex before/after graph for 20+ plugins

### Fixture: gopher-darnified-ui.json

Models DarNified UI collection. Contains:
- **DarNified UI** (NexusMods source, FOMOD with multi-step choices: font size, HUD layout, dialog style)
- **UIO - User Interface Organizer** (NexusMods source, NVSE plugin)
- **The Mod Configuration Menu** (NexusMods source, NVSE plugin + .esp)
- **modRules**: UIO loads before DarNified, MCM loads after

### Fixture: gopher-qol-tweaks.json

Models gameplay/QoL tweaks. Contains:
- **Stewie Tweaks** (NexusMods source, NVSE plugin + INI config)
- **Faster Pip-Boy Animation** (NexusMods source, NVSE plugin)
- **Simple DLC Delay** (NexusMods source, .esp)
- **INI tweaks** (bundle source, Fallout.ini and FalloutPrefs.ini modifications)
- **modRules**: plugin ordering for gameplay balance

### Test Cases

```csharp
public class GopherCollectionParsingTests
{
    // === Source Type Routing ===

    [Theory]
    [InlineData("gopher-stable-nv.json")]
    [InlineData("gopher-remaster.json")]
    [InlineData("gopher-darnified-ui.json")]
    [InlineData("gopher-qol-tweaks.json")]
    public void AllModsHaveValidSourceType(string fixture)
    // Verify every mod in the collection has a recognized ModSourceType

    [Theory]
    [InlineData("gopher-stable-nv.json", 2)]   // xNVSE + 4GB patcher
    [InlineData("gopher-remaster.json", 0)]
    public void ExternalDownloadsIdentifiedCorrectly(string fixture, int expectedCount)
    // Count Browse/Direct sources and verify they match expected external tools

    [Fact]
    public void BundledPatchesRouteToCorrectInstaller()
    // Verify bundle-source mods in gopher-remaster use FallbackCollectionDownloadInstaller

    // === FOMOD Handling ===

    [Fact]
    public void DarnifiedUiFomodChoicesParseSucessfully()
    // Parse gopher-darnified-ui.json and verify FOMOD choices structure is valid

    [Fact]
    public void RemasterFomodChoicesParseSuccessfully()
    // Parse gopher-remaster.json FOMADs (ILO, EVE) and verify choices

    // === Plugin Ordering ===

    [Theory]
    [InlineData("gopher-stable-nv.json")]
    [InlineData("gopher-remaster.json")]
    [InlineData("gopher-qol-tweaks.json")]
    public void ModRulesProduceValidTopologicalOrder(string fixture)
    // Parse modRules, build constraint graph, verify no cycles, verify topological sort succeeds

    [Fact]
    public void PluginOrderRespectsBeforeAfterConstraints()
    // Verify that after sorting, every "before" rule has source index < target index

    // === xNVSE Detection ===

    [Fact]
    public void StableNvDetectsNvsePlugins()
    // Verify mods with Data/NVSE/Plugins/*.dll paths trigger xNVSE plugin detection

    [Fact]
    public void QolTweaksDetectsStewiePluginAndIni()
    // Verify Stewie Tweaks .dll and .ini both detected correctly

    // === INI Handling ===

    [Fact]
    public void QolTweaksBundledInisDetectedAsIniTweaks()
    // Verify bundled INI files (Fallout.ini, FalloutPrefs.ini) trigger INI tweak detection

    // === BSA Validation ===

    [Fact]
    public void RemasterBsaArchivesHaveMatchingPlugins()
    // Verify every BSA in gopher-remaster has a corresponding .esp/.esm

    // === Proton/Steam Deck ===

    [Fact]
    public void StableNvTriggersProtontricksDiagnosticOnLinux()
    // With Linux filesystem mock, verify xNVSE plugins trigger ProtonRequirementsEmitter

    [Fact]
    public void FourGbPatcherTriggersProtonWarningOnLinux()
    // Verify 4GB patcher backup triggers Proton compatibility warning
}
```

## Implementation Order

| Step | Component | Depends On | Estimated Files |
|------|-----------|------------|-----------------|
| 1 | Collection JSON test fixtures | Nothing | 4 JSON files |
| 2 | Collection parsing tests (source routing, FOMOD, modRules) | Step 1 | 1 test class |
| 3 | FOMOD path normalization | Nothing | 1 modified file |
| 4 | FNV plugin sort order variety | Nothing | 1 new + 1 modified |
| 5 | Proton diagnostics emitter | Nothing | 2 new + 1 modified |
| 6 | xNVSE/BSA/INI detection tests | Steps 1-2 | Extend test class |
| 7 | Proton diagnostic tests | Step 5 | Extend test class |

Steps 3, 4, and 5 are independent and can run in parallel.

## Success Criteria

1. All four collection fixtures parse without error
2. Every mod routes to the correct installer based on source type
3. FOMOD choices apply without double-prefixed paths
4. ModRules produce valid topological plugin ordering
5. xNVSE plugins, INI tweaks, and BSA archives trigger correct diagnostics
6. Proton requirements emit warnings when xNVSE/4GB patcher detected on Linux
7. Full test suite passes (existing + new tests)

## Risk: Real Collection Drift

These test fixtures model Gopher's collections based on known mod lists and common FNV modding patterns. If the actual collection.json structures differ (unusual source types, unexpected FOMOD layouts), the tests will need fixture updates. Mitigate by downloading the real collection.json files via the Nexus API when an API key is available and comparing against fixtures.
