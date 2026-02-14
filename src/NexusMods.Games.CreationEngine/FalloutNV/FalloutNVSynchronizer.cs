using NexusMods.Games.CreationEngine.Abstractions;

namespace NexusMods.Games.CreationEngine.FalloutNV;

public class FalloutNVSynchronizer : ACreationEngineSynchronizer
{
    public FalloutNVSynchronizer(IServiceProvider provider, ICreationEngineGame game)
        : base(provider, game)
    {
    }
}
