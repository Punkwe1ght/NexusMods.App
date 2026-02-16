using DynamicData.Kernel;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Games.CreationEngine.Abstractions;
using NexusMods.Games.CreationEngine.FalloutNV.SortOrder;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;
using NexusMods.Sdk.Settings;

namespace NexusMods.Games.CreationEngine.FalloutNV;

public class FalloutNVSynchronizer : ACreationEngineSynchronizer
{
    private readonly FalloutNVSettings _settings;

    private static readonly GamePath GameFolder = new(LocationId.Game, "");

    public FalloutNVSynchronizer(IServiceProvider provider, ICreationEngineGame game)
        : base(provider, game, CreatePluginOrderProvider(provider))
    {
        _settings = provider.GetRequiredService<ISettingsManager>().Get<FalloutNVSettings>();
    }

    public override bool IsIgnoredBackupPath(GamePath path)
    {
        if (_settings.DoFullGameBackup)
            return false;

        // Ignore all game folder files from backup. Files are still tracked
        // (ingested into the loadout) for change detection — they just aren't
        // copied to the backup store. This matches the BG3 synchronizer pattern.
        return path.InFolder(GameFolder);
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
