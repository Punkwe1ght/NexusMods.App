using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Abstractions.Loadouts.Extensions;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Games.CreationEngine.FalloutNV.Models;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

/// <summary>
/// Warns when xNVSE is present but a tagged NvsePluginLoadoutItem has version metadata
/// suggesting it requires a newer xNVSE than what is installed.
/// </summary>
public class NvseVersionMismatchEmitter : ILoadoutDiagnosticEmitter
{
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

        // Only check if xNVSE is present
        var hasNvse = (syncTree.ContainsKey(NvseLoader) && syncTree[NvseLoader].HaveLoadout) ||
                      loadout.InstallationInstance.Locations.ToAbsolutePath(NvseLoader).FileExists;

        if (!hasNvse) yield break;

        // Get xNVSE loader version from disk
        var nvseVersion = GetNvseVersion(loadout);

        // Check all tagged NVSE plugin items
        foreach (var datom in loadout.Db.Datoms(NvsePluginLoadoutItem.NvsePlugin))
        {
            var nvseItem = NvsePluginLoadoutItem.Load(loadout.Db, datom.E);
            if (!nvseItem.IsValid()) continue;

            var loadoutItem = nvseItem.AsLoadoutItemGroup().AsLoadoutItem();
            if (loadoutItem.LoadoutId.Value != loadout.Id.Value) continue;
            if (!loadoutItem.IsEnabled()) continue;

            // Skip if no version metadata available
            if (!nvseItem.Contains(NvsePluginLoadoutItem.PluginVersion)) continue;

            var requiredVersionOpt = nvseItem.PluginVersion;
            if (!requiredVersionOpt.HasValue) continue;
            var requiredVersion = requiredVersionOpt.Value;
            if (string.IsNullOrEmpty(requiredVersion)) continue;

            // If we can't determine the installed xNVSE version, warn conservatively
            if (nvseVersion is null ||
                !Version.TryParse(requiredVersion, out var required) ||
                !Version.TryParse(nvseVersion, out var installed) ||
                installed < required)
            {
                yield return FalloutNVDiagnostics.CreateNvseVersionMismatch(
                    PluginName: loadoutItem.Name);
            }
        }
    }

    private static string? GetNvseVersion(Loadout.ReadOnly loadout)
    {
        try
        {
            var loaderPath = loadout.InstallationInstance.Locations.ToAbsolutePath(NvseLoader);
            if (!loaderPath.FileExists) return null;
            var fvi = loaderPath.FileInfo.GetFileVersionInfo();
            return fvi.FileVersion?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
