using System.Runtime.CompilerServices;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Diagnostics.Emitters;
using NexusMods.Paths;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Emitters;

public class FourGbPatcherEmitter : ILoadoutDiagnosticEmitter
{
    public async IAsyncEnumerable<Diagnostic> Diagnose(
        Loadout.ReadOnly loadout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        var gamePath = loadout.InstallationInstance.Locations[LocationId.Game].Path;
        var backupExe = gamePath / "FalloutNV_backup.exe";

        if (backupExe.FileExists)
            yield break;

        yield return FalloutNVDiagnostics.CreateFourGbPatcherNotDetected();
    }
}
