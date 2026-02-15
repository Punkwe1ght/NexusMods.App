using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Games.CreationEngine.FalloutNV.Emitters;
using NexusMods.Games.TestFramework;
using NexusMods.StandardGameLocators.TestHelpers;
using Xunit.Abstractions;

namespace NexusMods.Games.CreationEngine.Tests.FalloutNV;

public class NvseVersionMismatchEmitterTests(ITestOutputHelper outputHelper)
    : ALoadoutDiagnosticEmitterTest<NvseVersionMismatchEmitterTests, CreationEngine.FalloutNV.FalloutNV, NvseVersionMismatchEmitter>(outputHelper)
{
    protected override IServiceCollection AddServices(IServiceCollection services)
    {
        return base.AddServices(services)
            .AddCreationEngine()
            .AddUniversalGameLocator<CreationEngine.FalloutNV.FalloutNV>(new Version("1.4.0.525"));
    }

    [Fact]
    public async Task WhenNoNvsePresent_NoDiagnostics()
    {
        var loadout = await CreateLoadout();
        await ShouldHaveNoDiagnostics(loadout, because: "No xNVSE present");
    }
}
