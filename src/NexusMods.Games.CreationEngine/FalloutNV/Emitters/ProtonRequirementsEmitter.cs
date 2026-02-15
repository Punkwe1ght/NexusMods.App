using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Games.Generic.Dependencies;
using NexusMods.Paths;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

/// <summary>
/// Emits diagnostics when FNV runs under Proton/Wine on Linux and xNVSE or 4GB patcher
/// require additional configuration (protontricks, Wine DLL overrides).
/// </summary>
public class ProtonRequirementsEmitter : ILoadoutDiagnosticEmitter
{
    private static readonly GamePath NvsePluginsDir =
        new(LocationId.Game, "Data/NVSE/Plugins");
    private static readonly GamePath FourGbBackup =
        new(LocationId.Game, "FalloutNV_backup.exe");

    private readonly AggregateProtontricksDependency? _protontricks;
    private readonly bool _isLinux;

    public ProtonRequirementsEmitter(IServiceProvider serviceProvider)
        : this(serviceProvider, FileSystem.Shared.OS.IsLinux) { }

    /// <summary>
    /// Constructor for testing — allows overriding the Linux detection.
    /// </summary>
    internal ProtonRequirementsEmitter(IServiceProvider serviceProvider, bool isLinux)
    {
        _protontricks = serviceProvider.GetService<AggregateProtontricksDependency>();
        _isLinux = isLinux;
    }

    public IAsyncEnumerable<Diagnostic> Diagnose(
        Loadout.ReadOnly loadout, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<Diagnostic> Diagnose(
        Loadout.ReadOnly loadout,
        FrozenDictionary<GamePath, SyncNode> syncTree,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_isLinux)
            yield break;

        // Check for xNVSE plugins in loadout
        var nvseModCount = syncTree.Count(node =>
            node.Value.HaveLoadout &&
            node.Key.InFolder(NvsePluginsDir) &&
            node.Key.Extension == new Extension(".dll"));

        // If xNVSE plugins present, check protontricks availability
        if (nvseModCount > 0 && _protontricks is not null)
        {
            var installInfo = await _protontricks.QueryInstallationInformation(cancellationToken);
            if (!installInfo.HasValue)
                yield return FalloutNVDiagnostics.CreateXnvseRequiresProtontricks(NvseModCount: nvseModCount);
        }

        // Check for 4GB patcher under Proton
        var hasFourGbBackup = syncTree.ContainsKey(FourGbBackup) && syncTree[FourGbBackup].HaveLoadout;
        if (!hasFourGbBackup)
        {
            var diskPath = loadout.InstallationInstance.Locations.ToAbsolutePath(FourGbBackup);
            hasFourGbBackup = diskPath.FileExists;
        }

        if (hasFourGbBackup)
            yield return FalloutNVDiagnostics.CreateFourGbPatcherProtonWarning();
    }
}
