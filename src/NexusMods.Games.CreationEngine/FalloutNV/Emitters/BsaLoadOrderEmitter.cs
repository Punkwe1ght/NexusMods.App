using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

/// <summary>
/// Warns when a BSA archive in Data/ has no matching plugin (.esp or .esm) with the same filename stem.
/// FNV only loads BSA archives that match a loaded plugin name.
/// </summary>
public class BsaLoadOrderEmitter : ILoadoutDiagnosticEmitter
{
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

        // Collect all plugin stems (without extension)
        var pluginStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, node) in syncTree)
        {
            if (!node.HaveLoadout) continue;
            if (path.Parent != KnownPaths.Data) continue;

            if (path.Extension == KnownCEExtensions.ESP ||
                path.Extension == KnownCEExtensions.ESM)
            {
                pluginStems.Add(Path.GetFileNameWithoutExtension(path.FileName.ToString()));
            }
        }

        // Check each BSA for a matching plugin
        foreach (var (path, node) in syncTree)
        {
            if (!node.HaveLoadout) continue;
            if (path.Parent != KnownPaths.Data) continue;
            if (path.Extension != KnownCEExtensions.BSA) continue;

            var bsaStem = Path.GetFileNameWithoutExtension(path.FileName.ToString());
            if (pluginStems.Contains(bsaStem)) continue;

            yield return FalloutNVDiagnostics.CreateOrphanedBsa(
                BsaName: path.FileName.ToString());
        }
    }
}
