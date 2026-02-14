using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Games.TestFramework;
using NexusMods.HyperDuck;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.NexusModsApi;
using NexusMods.StandardGameLocators.TestHelpers;
using Xunit.Abstractions;

namespace NexusMods.Games.CreationEngine.Tests.FalloutNV;

public class LibraryArchiveInstallerTests(ITestOutputHelper outputHelper)
    : AIsolatedGameTest<LibraryArchiveInstallerTests, CreationEngine.FalloutNV.FalloutNV>(outputHelper)
{
    protected override IServiceCollection AddServices(IServiceCollection services)
    {
        return base.AddServices(services)
            .AddCreationEngine()
            .AddUniversalGameLocator<CreationEngine.FalloutNV.FalloutNV>(new Version("1.4.0.525"));
    }

    [Theory]
    // xNVSE - script extender with DLLs in root
    [InlineData("xNVSE", 67809, 327451)]
    // JIP LN NVSE Plugin - NVSE plugin in Data/NVSE/Plugins
    [InlineData("JIP LN NVSE Plugin", 58277, 327381)]
    [Trait("RequiresNetworking", "True")]
    public async Task CanInstallMod(string name, uint modId, uint fileId)
    {
        var loadout = await CreateLoadout();
        await using var tempFile = TemporaryFileManager.CreateFile();
        var download = await NexusModsLibrary.CreateDownloadJob(
            tempFile.Path,
            CreationEngine.FalloutNV.FalloutNV.NexusModsGameId.Value,
            ModId.From(modId),
            FileId.From(fileId));
        var libraryArchive = await LibraryService.AddDownload(download);

        var installed = await LoadoutManager.InstallItem(libraryArchive.AsLibraryItem(), loadout);

        var contents = installed.LoadoutItemGroup.Value.Children
            .OfTypeLoadoutItemWithTargetPath()
            .OfTypeLoadoutFile()
            .Select(child => ((GamePath)child.AsLoadoutItemWithTargetPath().TargetPath, child.Hash, child.Size))
            .OrderBy(x => x.Item1);

        await VerifyTable(contents, name);
    }
}
