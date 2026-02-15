using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Games.CreationEngine.FalloutNV.Installers;
using NexusMods.Games.TestFramework;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.NexusModsApi;
using NexusMods.StandardGameLocators.TestHelpers;
using Xunit.Abstractions;

namespace NexusMods.Games.CreationEngine.Tests.FalloutNV;

public class FnvModInstallerTests(ITestOutputHelper outputHelper)
    : ALibraryArchiveInstallerTests<FnvModInstallerTests, CreationEngine.FalloutNV.FalloutNV>(outputHelper)
{
    protected override IServiceCollection AddServices(IServiceCollection services)
    {
        return base.AddServices(services)
            .AddCreationEngine()
            .AddUniversalGameLocator<CreationEngine.FalloutNV.FalloutNV>(new Version("1.4.0.525"));
    }

    [Theory]
    [InlineData("JIP LN NVSE Plugin", 58277, 327381)]
    [Trait("RequiresNetworking", "True")]
    [Trait("RequiresApiKey", "True")]
    public async Task CanInstallNvseMod(string name, uint modId, uint fileId)
    {
        var loadout = await CreateLoadout();
        var libraryArchive = await DownloadArchiveFromNexusMods(ModId.From(modId), FileId.From(fileId));

        var installer = ServiceProvider.GetRequiredService<FnvModInstaller>();
        var group = await Install(installer, loadout, libraryArchive);
        var files = GetFiles(group).ToArray();

        files.Should().NotBeEmpty();
        await VerifyGroup(libraryArchive, group).UseParameters(modId, fileId);
    }
}
