using JetBrains.Annotations;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Generators.Diagnostics;

namespace NexusMods.Games.CreationEngine.FalloutNV;

internal static partial class FalloutNVDiagnostics
{
    private const string Source = "NexusMods.Games.CreationEngine.FalloutNV";

    [DiagnosticTemplate]
    [UsedImplicitly]
    internal static IDiagnosticTemplate ArchiveInvalidationDisabled = DiagnosticTemplateBuilder
        .Start()
        .WithId(new DiagnosticId(Source, number: 1))
        .WithTitle("Archive Invalidation Disabled")
        .WithSeverity(DiagnosticSeverity.Warning)
        .WithSummary("Archive invalidation is not enabled — asset mods (textures, meshes) will not load")
        .WithDetails("""
Archive invalidation allows loose mod files to override assets packed in the game's BSA archives.
Without it, texture replacers, mesh replacements, and other asset mods will not work.

Set `bInvalidateOlderFiles=1` in the `[Archive]` section of `FalloutCustom.ini`
(located in `Documents/My Games/FalloutNV/`).
""")
        .WithMessageData(messageBuilder => { })
        .Finish();

    [DiagnosticTemplate]
    [UsedImplicitly]
    internal static IDiagnosticTemplate FourGbPatcherNotDetected = DiagnosticTemplateBuilder
        .Start()
        .WithId(new DiagnosticId(Source, number: 2))
        .WithTitle("4GB Patcher Not Detected")
        .WithSeverity(DiagnosticSeverity.Warning)
        .WithSummary("The 4GB patcher has not been applied — the game may crash with heavy mod loads")
        .WithDetails("""
Fallout: New Vegas is a 32-bit application limited to 2GB of RAM by default. The 4GB patcher
enables Large Address Aware mode, allowing the game to use up to 4GB. This dramatically
improves stability with mods.

Download and run the FNV 4GB Patcher from Nexus Mods. After patching, `FalloutNV_backup.exe`
will appear in the game folder.
""")
        .WithMessageData(messageBuilder => { })
        .Finish();

    [DiagnosticTemplate]
    [UsedImplicitly]
    internal static IDiagnosticTemplate PluginLimitWarning = DiagnosticTemplateBuilder
        .Start()
        .WithId(new DiagnosticId(Source, number: 3))
        .WithTitle("Approaching Plugin Limit")
        .WithSeverity(DiagnosticSeverity.Warning)
        .WithSummary("Loadout has {PluginCount} plugins — the engine limit without Mod Limit Fix is 139")
        .WithDetails("""
Fallout: New Vegas has a hard cap of 255 plugins (.esp + .esm). Without the Mod Limit Fix
NVSE plugin, the functional limit is 139 due to an engine bug. Loading more than 139 plugins
without Mod Limit Fix causes crashes and save corruption.

Install Mod Limit Fix from Nexus Mods, or reduce your plugin count.
""")
        .WithMessageData(messageBuilder => messageBuilder
            .AddValue<int>("PluginCount")
        )
        .Finish();

    [DiagnosticTemplate]
    [UsedImplicitly]
    internal static IDiagnosticTemplate PluginLimitExceeded = DiagnosticTemplateBuilder
        .Start()
        .WithId(new DiagnosticId(Source, number: 4))
        .WithTitle("Plugin Limit Exceeded")
        .WithSeverity(DiagnosticSeverity.Critical)
        .WithSummary("Loadout has {PluginCount} plugins — exceeds the hard limit of 255")
        .WithDetails("""
Fallout: New Vegas cannot load more than 255 plugins (.esp + .esm combined). The game will
not start or will crash immediately with this many plugins.

Remove or merge plugins to bring the count below 255.
""")
        .WithMessageData(messageBuilder => messageBuilder
            .AddValue<int>("PluginCount")
        )
        .Finish();

    [DiagnosticTemplate]
    [UsedImplicitly]
    internal static IDiagnosticTemplate ModLimitFixMissing = DiagnosticTemplateBuilder
        .Start()
        .WithId(new DiagnosticId(Source, number: 5))
        .WithTitle("Mod Limit Fix Not Installed")
        .WithSeverity(DiagnosticSeverity.Warning)
        .WithSummary("Loadout has {PluginCount} plugins but Mod Limit Fix is not installed")
        .WithDetails("""
With {PluginCount} plugins loaded, you have exceeded the vanilla engine's functional limit
of 139 plugins. Without Mod Limit Fix, this causes crashes, save corruption, and FPS drops.

Mod Limit Fix is an NVSE plugin that raises the functional limit to 255 and also improves
loading times and reduces stutter. Install it from Nexus Mods.
""")
        .WithMessageData(messageBuilder => messageBuilder
            .AddValue<int>("PluginCount")
        )
        .Finish();

    [DiagnosticTemplate]
    [UsedImplicitly]
    internal static IDiagnosticTemplate XnvseMissing = DiagnosticTemplateBuilder
        .Start()
        .WithId(new DiagnosticId(Source, number: 6))
        .WithTitle("xNVSE Not Detected")
        .WithSeverity(DiagnosticSeverity.Warning)
        .WithSummary("xNVSE is not installed but {NvseModCount} mods in the loadout require it")
        .WithDetails("""
{NvseModCount} mods in the loadout contain NVSE plugins (files in `Data/NVSE/Plugins/`).
These mods require xNVSE (New Vegas Script Extender) to function. Without xNVSE, these
mods will silently fail to load.

Download xNVSE from the xNVSE GitHub releases page and extract it to the game folder.
""")
        .WithMessageData(messageBuilder => messageBuilder
            .AddValue<int>("NvseModCount")
        )
        .Finish();
}
