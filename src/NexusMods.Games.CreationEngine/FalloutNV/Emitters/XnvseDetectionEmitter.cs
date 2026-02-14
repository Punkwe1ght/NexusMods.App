using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Paths;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

public class XnvseDetectionEmitter : ILoadoutDiagnosticEmitter
{
    private static readonly GamePath NvsePluginsDir =
        new(LocationId.Game, "Data/NVSE/Plugins");
    private static readonly GamePath NvseLoader =
        new(LocationId.Game, "nvse_loader.exe");

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
        await Task.Yield();

        // Count mods that install files into Data/NVSE/Plugins/
        var nvseModCount = syncTree.Count(node =>
            node.Value.HaveLoadout &&
            node.Key.InFolder(NvsePluginsDir) &&
            node.Key.Extension == new Extension(".dll"));

        if (nvseModCount == 0)
            yield break;

        // Check if xNVSE loader is present (in loadout or on disk)
        var hasNvseInLoadout = syncTree.ContainsKey(NvseLoader) &&
                               syncTree[NvseLoader].HaveLoadout;
        if (hasNvseInLoadout)
            yield break;

        var diskPath = loadout.InstallationInstance.Locations.ToAbsolutePath(NvseLoader);
        if (diskPath.FileExists)
            yield break;

        yield return FalloutNVDiagnostics.CreateXnvseMissing(NvseModCount: nvseModCount);
    }
}
