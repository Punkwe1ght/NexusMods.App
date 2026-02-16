using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Paths;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

/// <summary>
/// Warns when a BSA archive in Data/ has no matching plugin (.esp or .esm) with the same filename stem.
/// FNV loads BSAs three ways: SArchiveList in INI, .nam files for DLCs, and plugin name matching.
/// This emitter accounts for all three to avoid false positives on vanilla/DLC BSAs.
/// </summary>
public class BsaLoadOrderEmitter : ILoadoutDiagnosticEmitter
{
    private static readonly Extension NAM = new(".nam");

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
        // Collect .nam file stems — DLC BSAs starting with these stems are loaded automatically
        var namStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, node) in syncTree)
        {
            if (!node.HaveLoadout) continue;
            if (path.Parent != KnownPaths.Data) continue;

            if (path.Extension == KnownCEExtensions.ESP ||
                path.Extension == KnownCEExtensions.ESM)
            {
                pluginStems.Add(Path.GetFileNameWithoutExtension(path.FileName.ToString()));
            }
            else if (path.Extension == NAM)
            {
                namStems.Add(Path.GetFileNameWithoutExtension(path.FileName.ToString()));
            }
        }

        // Read SArchiveList from INI to get explicitly listed BSAs
        var sArchiveList = ReadSArchiveList(loadout);

        // Check each BSA for a matching loading mechanism
        foreach (var (path, node) in syncTree)
        {
            if (!node.HaveLoadout) continue;
            if (path.Parent != KnownPaths.Data) continue;
            if (path.Extension != KnownCEExtensions.BSA) continue;

            var bsaFileName = path.FileName.ToString();
            var bsaStem = Path.GetFileNameWithoutExtension(bsaFileName);

            // 1. BSA stem matches a loaded plugin exactly
            if (pluginStems.Contains(bsaStem)) continue;

            // 2. BSA is listed in SArchiveList
            if (sArchiveList.Contains(bsaFileName)) continue;

            // 3. BSA belongs to a DLC with a .nam file (e.g. "DeadMoney - Main.bsa" matches "DeadMoney.nam")
            if (MatchesNamFile(bsaStem, namStems)) continue;

            // 4. Update.bsa is always loaded by the engine
            if (bsaStem.Equals("Update", StringComparison.OrdinalIgnoreCase)) continue;

            yield return FalloutNVDiagnostics.CreateOrphanedBsa(
                BsaName: bsaFileName);
        }
    }

    /// <summary>
    /// Checks if a BSA stem starts with any .nam file stem.
    /// E.g. "DeadMoney - Main" starts with "DeadMoney" from DEADMONEY.NAM.
    /// </summary>
    private static bool MatchesNamFile(string bsaStem, HashSet<string> namStems)
    {
        foreach (var nam in namStems)
        {
            if (bsaStem.StartsWith(nam, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Reads SArchiveList from INI files. Checks Preferences location first (FalloutCustom.ini,
    /// Fallout.ini), then falls back to Fallout_default.ini in the game root.
    /// Returns a set of BSA filenames listed in the INI.
    /// </summary>
    private static HashSet<string> ReadSArchiveList(Loadout.ReadOnly loadout)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var locations = loadout.InstallationInstance.Locations;
            var prefsPath = locations[LocationId.Preferences].Path;
            var gamePath = locations[LocationId.Game].Path;

            // Check Preferences location first, then fall back to game root default INI
            var archiveList =
                TryReadSArchiveList(prefsPath / "FalloutCustom.ini") ??
                TryReadSArchiveList(prefsPath / "Fallout.ini") ??
                TryReadSArchiveList(gamePath / "Fallout_default.ini");

            if (archiveList != null)
            {
                foreach (var entry in archiveList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    result.Add(entry);
                }
            }
        }
        catch
        {
            // If we can't read the INI, just skip SArchiveList matching
        }

        return result;
    }

    private static string? TryReadSArchiveList(AbsolutePath iniPath)
    {
        if (!iniPath.FileExists) return null;

        try
        {
            var lines = File.ReadAllLines(iniPath.ToString());
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("SArchiveList", StringComparison.OrdinalIgnoreCase) &&
                    trimmed.Contains('='))
                {
                    return trimmed.Split('=', 2)[1].Trim();
                }
            }
        }
        catch
        {
            // Ignore read errors
        }

        return null;
    }
}
