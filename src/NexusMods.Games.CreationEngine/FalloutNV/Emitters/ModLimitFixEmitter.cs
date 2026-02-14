using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Paths;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

public class ModLimitFixEmitter : ILoadoutDiagnosticEmitter
{
    private const int VanillaFunctionalLimit = 139;
    private static readonly GamePath ModLimitFixPath =
        new(LocationId.Game, "Data/NVSE/Plugins/mod_limit_fix.dll");

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

        var pluginCount = syncTree.Count(node =>
            node.Value.HaveLoadout &&
            node.Key.Parent == KnownPaths.Data &&
            (node.Key.Extension == KnownCEExtensions.ESP ||
             node.Key.Extension == KnownCEExtensions.ESM));

        if (pluginCount <= VanillaFunctionalLimit)
            yield break;

        // Check if mod_limit_fix.dll is present in the loadout
        var hasModLimitFix = syncTree.ContainsKey(ModLimitFixPath) &&
                             syncTree[ModLimitFixPath].HaveLoadout;

        if (hasModLimitFix)
            yield break;

        // Also check disk in case it was installed manually
        var diskPath = loadout.InstallationInstance.Locations.ToAbsolutePath(ModLimitFixPath);
        if (diskPath.FileExists)
            yield break;

        yield return FalloutNVDiagnostics.CreateModLimitFixMissing(PluginCount: pluginCount);
    }
}
