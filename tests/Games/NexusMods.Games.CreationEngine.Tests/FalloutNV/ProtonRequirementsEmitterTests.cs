using System.Collections.Frozen;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Loadouts.Synchronizers;
using NexusMods.Games.CreationEngine.FalloutNV.Emitters;
using NexusMods.Games.TestFramework;
using NexusMods.Paths;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;
using NexusMods.StandardGameLocators.TestHelpers;
using Xunit.Abstractions;

namespace NexusMods.Games.CreationEngine.Tests.FalloutNV;

public class ProtonRequirementsEmitterTests(ITestOutputHelper outputHelper)
    : AIsolatedGameTest<ProtonRequirementsEmitterTests, CreationEngine.FalloutNV.FalloutNV>(outputHelper)
{
    protected override IServiceCollection AddServices(IServiceCollection services)
    {
        return base.AddServices(services)
            .AddCreationEngine()
            .AddUniversalGameLocator<CreationEngine.FalloutNV.FalloutNV>(new Version("1.4.0.525"));
    }

    private ProtonRequirementsEmitter CreateEmitter(bool isLinux) =>
        new(ServiceProvider, isLinux);

    private async Task<FrozenDictionary<GamePath, SyncNode>> BuildSyncTree(Loadout.ReadOnly loadout) =>
        (await Synchronizer.BuildSyncTree(loadout)).ToFrozenDictionary();

    [Fact]
    public async Task WhenNotLinux_NoDiagnostics()
    {
        var emitter = CreateEmitter(isLinux: false);
        var loadout = await CreateLoadout();

        // Add xNVSE plugins to prove that non-Linux still produces no diagnostics
        using var tx = Connection.BeginTransaction();
        var modGroup = AddEmptyGroup(tx, loadout, "xNVSE Mod");
        AddFile(tx, loadout, modGroup, new GamePath(LocationId.Game, "Data/NVSE/Plugins/test.dll"));
        await tx.Commit();
        loadout = Loadout.Load(Connection.Db, loadout.LoadoutId);

        var syncTree = await BuildSyncTree(loadout);
        var diagnostics = await emitter.Diagnose(loadout, syncTree, CancellationToken.None).ToArrayAsync();
        diagnostics.Should().BeEmpty(because: "non-Linux should short-circuit regardless of loadout contents");
    }

    [Fact]
    public async Task WhenLinuxAndNoNvsePlugins_NoProtontricksDiagnostic()
    {
        var emitter = CreateEmitter(isLinux: true);
        var loadout = await CreateLoadout();

        var syncTree = await BuildSyncTree(loadout);
        var diagnostics = await emitter.Diagnose(loadout, syncTree, CancellationToken.None).ToArrayAsync();
        diagnostics.Should().NotContain(d => d.Title == "Protontricks Required for xNVSE",
            because: "no xNVSE plugins means no protontricks diagnostic");
    }

    [Fact]
    public async Task WhenLinuxAndFourGbBackupPresent_EmitsFourGbWarning()
    {
        var emitter = CreateEmitter(isLinux: true);
        var loadout = await CreateLoadout();

        using var tx = Connection.BeginTransaction();
        var modGroup = AddEmptyGroup(tx, loadout, "4GB Patcher");
        AddFile(tx, loadout, modGroup, new GamePath(LocationId.Game, "FalloutNV_backup.exe"));
        await tx.Commit();
        loadout = Loadout.Load(Connection.Db, loadout.LoadoutId);

        var syncTree = await BuildSyncTree(loadout);
        var diagnostics = await emitter.Diagnose(loadout, syncTree, CancellationToken.None).ToArrayAsync();
        diagnostics.Should().ContainSingle(d => d.Title == "4GB Patcher Under Proton");
    }

    [Fact]
    public async Task WhenLinuxAndNoFourGbBackup_NoFourGbWarning()
    {
        var emitter = CreateEmitter(isLinux: true);
        var loadout = await CreateLoadout();

        var syncTree = await BuildSyncTree(loadout);
        var diagnostics = await emitter.Diagnose(loadout, syncTree, CancellationToken.None).ToArrayAsync();
        diagnostics.Should().NotContain(d => d.Title == "4GB Patcher Under Proton",
            because: "no 4GB patcher backup means no warning");
    }
}
