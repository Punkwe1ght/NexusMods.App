using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Games.CreationEngine.FalloutNV.Emitters;
using NexusMods.Games.TestFramework;
using NexusMods.StandardGameLocators.TestHelpers;
using Xunit.Abstractions;

namespace NexusMods.Games.CreationEngine.Tests.FalloutNV;

public class ArchiveInvalidationEmitterTests(ITestOutputHelper outputHelper)
    : ALoadoutDiagnosticEmitterTest<ArchiveInvalidationEmitterTests, CreationEngine.FalloutNV.FalloutNV, ArchiveInvalidationEmitter>(outputHelper)
{
    protected override IServiceCollection AddServices(IServiceCollection services)
    {
        return base.AddServices(services)
            .AddCreationEngine()
            .AddUniversalGameLocator<CreationEngine.FalloutNV.FalloutNV>(new Version("1.4.0.525"));
    }

    [Fact]
    public async Task WhenArchiveInvalidationDisabled_EmitsDiagnostic()
    {
        var loadout = await CreateLoadout();
        var diagnostics = await GetAllDiagnostics(loadout);
        diagnostics.Should().ContainSingle(d => d.Title == "Archive Invalidation Disabled");
    }
}
