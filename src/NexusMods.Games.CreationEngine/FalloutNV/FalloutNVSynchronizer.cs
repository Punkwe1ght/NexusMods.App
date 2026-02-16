using DynamicData.Kernel;
using Microsoft.Extensions.DependencyInjection;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Games.CreationEngine.Abstractions;
using NexusMods.Games.CreationEngine.FalloutNV.SortOrder;
using NexusMods.Paths;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;
using NexusMods.Sdk.Settings;

namespace NexusMods.Games.CreationEngine.FalloutNV;

public class FalloutNVSynchronizer : ACreationEngineSynchronizer
{
    private readonly FalloutNVSettings _settings;

    /// <summary>
    /// Root-level game directories that contain moddable files (outside Data/).
    /// These are backed up alongside Data/ when mods target them.
    /// </summary>
    private static readonly GamePath NvsePath = new(LocationId.Game, "NVSE");

    public FalloutNVSynchronizer(IServiceProvider provider, ICreationEngineGame game)
        : base(provider, game, CreatePluginOrderProvider(provider))
    {
        _settings = provider.GetRequiredService<ISettingsManager>().Get<FalloutNVSettings>();
    }

    public override bool IsIgnoredBackupPath(GamePath path)
    {
        if (_settings.DoFullGameBackup)
            return false;

        // Don't backup BSA archives
        if (path.Extension == KnownCEExtensions.BSA)
            return true;

        if (path.LocationId != LocationId.Game)
            return false;

        // Back up Data/ (where mods live) and NVSE/ (script extender plugins)
        if (path.InFolder(KnownPaths.Data) || path.InFolder(NvsePath))
            return false;

        // Ignore everything else (engine binaries, executables, video files, etc.)
        return true;
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
