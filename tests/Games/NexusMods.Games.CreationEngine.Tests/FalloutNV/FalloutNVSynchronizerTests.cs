using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Games.TestFramework;
using NexusMods.StandardGameLocators.TestHelpers;
using Xunit.Abstractions;

namespace NexusMods.Games.CreationEngine.Tests.FalloutNV;

public class FalloutNVSynchronizerTests(ITestOutputHelper outputHelper)
    : AIsolatedGameTest<FalloutNVSynchronizerTests, CreationEngine.FalloutNV.FalloutNV>(outputHelper)
{
    protected override IServiceCollection AddServices(IServiceCollection services)
    {
        return base.AddServices(services)
            .AddCreationEngine()
            .AddUniversalGameLocator<CreationEngine.FalloutNV.FalloutNV>(new Version("1.4.0.525"));
    }

    [Fact]
    public async Task CanCreateLoadout()
    {
        var loadout = await CreateLoadout();
        loadout.IsValid().Should().BeTrue();
    }

    [Fact]
    public void BsaFilesExcludedFromBackup()
    {
        var synchronizer = Synchronizer;
        var bsaPath = new NexusMods.Sdk.Games.GamePath(
            NexusMods.Sdk.Games.LocationId.Game, "Data/test.bsa");
        synchronizer.IsIgnoredBackupPath(bsaPath).Should().BeTrue();
    }

    [Fact]
    public void NonBsaFilesNotExcludedFromBackup()
    {
        var synchronizer = Synchronizer;
        var espPath = new NexusMods.Sdk.Games.GamePath(
            NexusMods.Sdk.Games.LocationId.Game, "Data/test.esp");
        synchronizer.IsIgnoredBackupPath(espPath).Should().BeFalse();
    }
}
