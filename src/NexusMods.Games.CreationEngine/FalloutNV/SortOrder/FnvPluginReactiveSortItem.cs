using DynamicData.Kernel;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.SortOrder;

/// <summary>
/// UI-facing reactive item for FNV plugin sort order.
/// </summary>
public class FnvPluginReactiveSortItem : IReactiveSortItem<FnvPluginReactiveSortItem, SortItemKey<string>>
{
    public FnvPluginReactiveSortItem(int sortIndex, string pluginFileName, string modName, bool isActive)
    {
        SortIndex = sortIndex;
        PluginFileName = pluginFileName;
        DisplayName = pluginFileName;
        ModName = modName;
        IsActive = isActive;
        Key = new SortItemKey<string>(pluginFileName);
    }

    public string PluginFileName { get; set; }

    public SortItemKey<string> Key { get; }

    public int SortIndex { get; set; }
    public string DisplayName { get; }
    public string ModName { get; set; }
    public Optional<LoadoutItemGroupId> ModGroupId { get; set; }
    public bool IsActive { get; set; }
    public ISortItemLoadoutData? LoadoutData { get; set; }
}
