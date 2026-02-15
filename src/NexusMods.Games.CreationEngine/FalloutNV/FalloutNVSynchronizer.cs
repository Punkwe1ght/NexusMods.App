using DynamicData.Kernel;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Games.CreationEngine.Abstractions;
using NexusMods.Games.CreationEngine.FalloutNV.SortOrder;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV;

public class FalloutNVSynchronizer : ACreationEngineSynchronizer
{
    public FalloutNVSynchronizer(IServiceProvider provider, ICreationEngineGame game)
        : base(provider, game, CreatePluginOrderProvider(provider))
    {
    }

    private static Func<LoadoutId, IReadOnlyList<string>?> CreatePluginOrderProvider(IServiceProvider provider)
    {
        var variety = provider.GetRequiredService<FnvPluginSortOrderVariety>();
        return loadoutId =>
        {
            var order = variety.GetPluginOrder(loadoutId, Optional<CollectionGroupId>.None);
            return order.Count > 0 ? order : null;
        };
    }
}
