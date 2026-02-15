using System.Runtime.CompilerServices;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Abstractions.Loadouts.Extensions;
using NexusMods.Games.CreationEngine.FalloutNV.Models;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

/// <summary>
/// Warns when multiple INI tweak files target the same INI and set the same key to different values.
/// </summary>
public class IniConflictEmitter : ILoadoutDiagnosticEmitter
{

    public IAsyncEnumerable<Diagnostic> Diagnose(
        Loadout.ReadOnly loadout, CancellationToken cancellationToken)
    {
        return DiagnoseImpl(loadout, cancellationToken);
    }

    private async IAsyncEnumerable<Diagnostic> DiagnoseImpl(
        Loadout.ReadOnly loadout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        // Group all INI tweak files by target INI
        var tweaksByTarget = new Dictionary<string, List<IniTweakLoadoutFile.ReadOnly>>(StringComparer.OrdinalIgnoreCase);

        foreach (var datom in loadout.Db.Datoms(IniTweakLoadoutFile.IniTweakFile))
        {
            var tweakFile = IniTweakLoadoutFile.Load(loadout.Db, datom.E);
            if (!tweakFile.IsValid()) continue;

            // Verify this tweak belongs to the current loadout
            var loadoutItem = tweakFile.AsLoadoutFile().AsLoadoutItemWithTargetPath().AsLoadoutItem();
            if (loadoutItem.LoadoutId.Value != loadout.Id.Value) continue;
            if (!loadoutItem.IsEnabled()) continue;

            var targetIni = tweakFile.TargetIniFile;
            if (!tweaksByTarget.TryGetValue(targetIni, out var list))
            {
                list = [];
                tweaksByTarget[targetIni] = list;
            }
            list.Add(tweakFile);
        }

        // Check each target INI for key conflicts
        foreach (var (targetIni, tweaks) in tweaksByTarget)
        {
            if (tweaks.Count < 2) continue;

            var allKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var tweak in tweaks)
            {
                var gamePath = (GamePath)tweak.AsLoadoutFile().AsLoadoutItemWithTargetPath().TargetPath;
                var path = loadout.InstallationInstance.Locations.ToAbsolutePath(gamePath);

                if (!path.FileExists) continue;

                try
                {
                    foreach (var line in File.ReadAllLines(path.ToString()))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length == 0 || trimmed.StartsWith('[') || trimmed.StartsWith(';'))
                            continue;

                        var eqIndex = trimmed.IndexOf('=');
                        if (eqIndex <= 0) continue;

                        var key = trimmed[..eqIndex].Trim();
                        var value = trimmed[(eqIndex + 1)..].Trim();

                        if (!allKeys.TryGetValue(key, out var values))
                        {
                            values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            allKeys[key] = values;
                        }
                        values.Add(value);
                    }
                }
                catch
                {
                    // Skip files that can't be read
                }
            }

            var conflictCount = allKeys.Count(kv => kv.Value.Count > 1);
            if (conflictCount > 0)
            {
                yield return FalloutNVDiagnostics.CreateIniConflict(
                    ConflictCount: conflictCount,
                    TargetIniFile: targetIni);
            }
        }
    }
}
