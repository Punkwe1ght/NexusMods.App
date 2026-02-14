using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

public class PluginLimitEmitter : ILoadoutDiagnosticEmitter
{
    private const int SoftLimit = 130;
    private const int HardLimit = 255;

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

        if (pluginCount >= HardLimit)
        {
            yield return FalloutNVDiagnostics.CreatePluginLimitExceeded(PluginCount: pluginCount);
        }
        else if (pluginCount >= SoftLimit)
        {
            yield return FalloutNVDiagnostics.CreatePluginLimitWarning(PluginCount: pluginCount);
        }
    }
}
