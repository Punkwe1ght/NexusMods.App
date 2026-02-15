using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Games.CreationEngine.FalloutNV.Emitters;
using NexusMods.Games.TestFramework;
using NexusMods.StandardGameLocators.TestHelpers;
using Xunit.Abstractions;

namespace NexusMods.Games.CreationEngine.Tests.FalloutNV;

public class BsaLoadOrderEmitterTests(ITestOutputHelper outputHelper)
    : ALoadoutDiagnosticEmitterTest<BsaLoadOrderEmitterTests, CreationEngine.FalloutNV.FalloutNV, BsaLoadOrderEmitter>(outputHelper)
{
    protected override IServiceCollection AddServices(IServiceCollection services)
    {
        return base.AddServices(services)
            .AddCreationEngine()
            .AddUniversalGameLocator<CreationEngine.FalloutNV.FalloutNV>(new Version("1.4.0.525"));
    }

    [Fact]
    public async Task WhenNoBsaFiles_NoDiagnostics()
    {
        var loadout = await CreateLoadout();
        await ShouldHaveNoDiagnostics(loadout, because: "No BSA files in loadout");
    }
}
