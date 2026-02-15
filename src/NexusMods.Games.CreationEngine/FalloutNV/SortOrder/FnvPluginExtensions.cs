using DynamicData;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Loadouts;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Loadouts;

namespace NexusMods.Games.CreationEngine.FalloutNV.SortOrder;

public static class FnvPluginExtensions
{
    public static FnvPluginSortOrderItem.ReadOnly[] RetrieveFnvPluginSortableEntries(this IDb db, SortOrderId sortOrderId)
    {
        return db.Datoms(SortOrderItem.ParentSortOrder, sortOrderId)
            .Select(d => FnvPluginSortOrderItem.Load(db, d.E))
            .Where(si => si.IsValid())
            .OrderBy(si => si.AsSortOrderItem().SortIndex)
            .ToArray();
    }

    public static IReadOnlyList<SortItemData<SortItemKey<string>>> RetrieveFnvPluginSortOrderItems(IDb db, SortOrderId sortOrderId)
    {
        return db.Connection.Query<(string PluginFileName, int SortIndex, EntityId ItemId)>($"""
            SELECT * FROM fnvplugin.FnvPluginSortOrderItems({db}, {sortOrderId.Value})
            """)
            .Select(row => new SortItemData<SortItemKey<string>>(
                new SortItemKey<string>(row.PluginFileName),
                row.SortIndex
            ))
            .ToList();
    }

    public static IEnumerable<(string PluginFileName, bool IsEnabled, string ModName, EntityId ModGroupId)> RetrieveWinningPluginsInLoadout(IDb db, LoadoutId loadoutId)
    {
        return db.Connection.Query<(string PluginFileName, bool IsEnabled, string ModName, EntityId ModGroupId)>($"""
            SELECT * FROM fnvplugin.WinningLoadoutPluginGroups({db.Connection}, {loadoutId}, {LocationId.Game.Value})
            """);
    }

    public static IObservable<IChangeSet<(string PluginFileName, int SortIndex, EntityId ItemId, bool? IsEnabled, string? ModName, EntityId? ModGroupId), SortItemKey<string>>>
        ObserveFnvPluginSortOrder(IConnection connection, SortOrderId sortOrderId, LoadoutId loadoutId)
    {
        return connection.Query<(string PluginFileName, int SortIndex, EntityId ItemId, bool? IsEnabled, string? ModName, EntityId? ModGroupId)>($"""
            SELECT * FROM fnvplugin.FnvPluginSortOrderWithLoadoutData({connection}, {sortOrderId.Value}, {loadoutId}, {LocationId.Game.Value})
            """)
            .Observe(table => new SortItemKey<string>(table.PluginFileName));
    }
}
