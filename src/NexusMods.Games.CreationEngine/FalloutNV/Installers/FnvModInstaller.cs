using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusMods.Abstractions.Library.Installers;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Games.CreationEngine.FalloutNV.Models;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.Paths;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Library;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.Installers;

/// <summary>
/// FNV-specific installer that detects xNVSE plugins and INI tweaks,
/// tagging them with custom data models. Falls through for archives
/// that contain neither pattern.
/// </summary>
public class FnvModInstaller : ALibraryArchiveInstaller
{
    private static readonly RelativePath NvsePluginsDir = "Data/NVSE/Plugins";
    private static readonly Extension DllExtension = new(".dll");
    private static readonly Extension IniExtension = new(".ini");

    private static readonly HashSet<string> KnownIniFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fallout.ini",
        "FalloutPrefs.ini",
        "FalloutCustom.ini",
        "GECKCustom.ini",
    };

    public FnvModInstaller(IServiceProvider serviceProvider)
        : base(serviceProvider, serviceProvider.GetRequiredService<ILogger<FnvModInstaller>>())
    {
    }

    public override ValueTask<InstallerResult> ExecuteAsync(
        LibraryArchive.ReadOnly libraryArchive,
        LoadoutItemGroup.New loadoutGroup,
        ITransaction transaction,
        Loadout.ReadOnly loadout,
        CancellationToken cancellationToken)
    {
        var nvsePluginFiles = new List<LibraryArchiveFileEntry.ReadOnly>();
        var iniTweakFiles = new List<(LibraryArchiveFileEntry.ReadOnly Entry, string TargetIni)>();

        // Classify files
        foreach (var fileEntry in libraryArchive.Children)
        {
            var path = fileEntry.Path;

            // Detect xNVSE plugins: files in Data/NVSE/Plugins/*.dll
            if (path.Extension == DllExtension && path.InFolder(NvsePluginsDir))
            {
                nvsePluginFiles.Add(fileEntry);
            }

            // Detect INI tweaks: .ini files with known FNV INI filenames
            if (path.Extension == IniExtension)
            {
                var fileName = path.FileName.ToString();
                if (KnownIniFiles.Contains(fileName))
                {
                    iniTweakFiles.Add((fileEntry, fileName));
                }
            }
        }

        // Fall through if no special files detected
        if (nvsePluginFiles.Count == 0 && iniTweakFiles.Count == 0)
            return ValueTask.FromResult<InstallerResult>(new NotSupported(Reason: "No xNVSE plugins or INI tweaks detected"));

        // Install all files from the archive
        foreach (var fileEntry in libraryArchive.Children)
        {
            var gamePath = new GamePath(LocationId.Game, fileEntry.Path);

            var loadoutFile = new LoadoutFile.New(transaction, out var entityId)
            {
                Hash = fileEntry.AsLibraryFile().Hash,
                Size = fileEntry.AsLibraryFile().Size,
                LoadoutItemWithTargetPath = new LoadoutItemWithTargetPath.New(transaction, entityId)
                {
                    TargetPath = gamePath.ToGamePathParentTuple(loadout),
                    LoadoutItem = new LoadoutItem.New(transaction, entityId)
                    {
                        Name = fileEntry.AsLibraryFile().FileName,
                        LoadoutId = loadout,
                        ParentId = loadoutGroup,
                    },
                },
            };

            // Tag INI tweaks with model
            if (iniTweakFiles.Any(x => x.Entry.Id == fileEntry.Id))
            {
                var targetIni = iniTweakFiles.First(x => x.Entry.Id == fileEntry.Id).TargetIni;
                _ = new IniTweakLoadoutFile.New(transaction, entityId)
                {
                    IsIniTweakFile = true,
                    TargetIniFile = targetIni,
                    LoadoutFile = loadoutFile,
                };
            }
        }

        // Tag the group as an xNVSE plugin if applicable
        if (nvsePluginFiles.Count > 0)
        {
            _ = new NvsePluginLoadoutItem.New(transaction, loadoutGroup.Id)
            {
                IsNvsePlugin = true,
                LoadoutItemGroup = loadoutGroup,
            };
        }

        return ValueTask.FromResult<InstallerResult>(new Success());
    }
}
