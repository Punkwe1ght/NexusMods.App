using System.Runtime.CompilerServices;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Paths;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;
using NexusMods.Sdk.Settings;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

public class ArchiveInvalidationEmitter : ILoadoutDiagnosticEmitter
{
    private readonly ISettingsManager _settingsManager;

    public ArchiveInvalidationEmitter(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public async IAsyncEnumerable<Diagnostic> Diagnose(
        Loadout.ReadOnly loadout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        var settings = _settingsManager.Get<FalloutNVSettings>();
        if (!settings.CheckArchiveInvalidation)
            yield break;

        var prefsPath = loadout.InstallationInstance.Locations[LocationId.Preferences].Path;

        // Check FalloutCustom.ini first (preferred), then Fallout.ini
        var customIni = prefsPath / "FalloutCustom.ini";
        var falloutIni = prefsPath / "Fallout.ini";

        if (HasArchiveInvalidation(customIni) || HasArchiveInvalidation(falloutIni))
            yield break;

        yield return FalloutNVDiagnostics.CreateArchiveInvalidationDisabled();
    }

    private static bool HasArchiveInvalidation(AbsolutePath iniPath)
    {
        if (!iniPath.FileExists) return false;

        try
        {
            var lines = File.ReadAllLines(iniPath.ToString());
            return lines.Any(line =>
                line.Trim().StartsWith("bInvalidateOlderFiles", StringComparison.OrdinalIgnoreCase) &&
                line.Contains('=') &&
                line.Split('=')[1].Trim() == "1");
        }
        catch
        {
            return false;
        }
    }
}
