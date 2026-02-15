using System.Reactive.Linq;
using DynamicData;
using DynamicData.Kernel;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Loadouts;
using NexusMods.Abstractions.Loadouts.Synchronizers.Conflicts;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.MnemonicDB.Abstractions.TxFunctions;
using NexusMods.Sdk.Loadouts;
using OneOf;
using LoadoutSortOrder = NexusMods.Abstractions.Loadouts.SortOrder;

namespace NexusMods.Games.CreationEngine.FalloutNV.SortOrder;

public class FnvPluginSortOrderVariety : ASortOrderVariety<
    SortItemKey<string>,
    FnvPluginReactiveSortItem,
    SortItemLoadoutData<SortItemKey<string>>,
    SortItemData<SortItemKey<string>>>
{
    private static readonly SortOrderVarietyId StaticVarietyId = SortOrderVarietyId.From(new Guid("297531FB-FDB8-4F56-8201-38F132F2A4D1"));

    public override SortOrderVarietyId SortOrderVarietyId => StaticVarietyId;

    public FnvPluginSortOrderVariety(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public override SortOrderUiMetadata SortOrderUiMetadata { get; } = new()
    {
        SortOrderName = "Plugin Load Order",
        OverrideInfoTitle = "Plugin Load Order for Fallout: New Vegas - Last Loaded Plugin Wins",
        OverrideInfoMessage = """
                               Fallout: New Vegas plugins (.esp/.esm) modify game data. When two plugins change the same record, the one loaded last takes priority.
                               For example, the last position overwrites earlier positions.
                               """,
        WinnerIndexToolTip = "Last Loaded Plugin Wins: Plugins loaded later will overwrite changes from plugins loaded before them.",
        IndexColumnHeader = "Load Order",
        DisplayNameColumnHeader = "Plugin Name",
        EmptyStateMessageTitle = "No plugins detected",
        EmptyStateMessageContents = "Mods that contain .esp or .esm plugin files will appear here for load order configuration.",
        LearnMoreUrl = "",
    };

    public override async ValueTask<SortOrderId> GetOrCreateSortOrderFor(
        LoadoutId loadoutId,
        OneOf<LoadoutId, CollectionGroupId> parentEntity,
        CancellationToken token = default)
    {
        var optionalSortOrderId = GetSortOrderIdFor(parentEntity);
        if (optionalSortOrderId.HasValue)
            return optionalSortOrderId.Value;

        token.ThrowIfCancellationRequested();

        using var ts = Connection.BeginTransaction();
        var newSortOrder = new LoadoutSortOrder.New(ts)
        {
            LoadoutId = loadoutId,
            ParentEntity = parentEntity,
            SortOrderTypeId = SortOrderVarietyId.Value,
        };

        var newFnvPluginSortOrder = new FnvPluginSortOrder.New(ts, newSortOrder)
        {
            SortOrder = newSortOrder,
            IsMarker = true,
        };

        var commitResult = await ts.Commit();

        var fnvPluginSortOrder = commitResult.Remap(newFnvPluginSortOrder);
        return fnvPluginSortOrder.AsSortOrder().SortOrderId;
    }

    public override IObservable<IChangeSet<FnvPluginReactiveSortItem, SortItemKey<string>>> GetSortOrderItemsChangeSet(SortOrderId sortOrderId)
    {
        var sortOrder = LoadoutSortOrder.Load(Connection.Db, sortOrderId);
        if (!sortOrder.IsValid())
            return Observable.Empty<IChangeSet<FnvPluginReactiveSortItem, SortItemKey<string>>>();

        var loadoutId = sortOrder.LoadoutId;

        var result = FnvPluginExtensions.ObserveFnvPluginSortOrder(Connection, sortOrderId, loadoutId)
            .Transform(row =>
                {
                    var model = new FnvPluginReactiveSortItem(
                        row.SortIndex,
                        row.PluginFileName,
                        modName: row.ModName ?? row.PluginFileName,
                        isActive: row.IsEnabled ?? false
                    );

                    if (row.ModGroupId == null) return model;

                    model.ModGroupId = LoadoutItemGroupId.From(row.ModGroupId.Value);
                    var loadoutData = new SortItemLoadoutData<SortItemKey<string>>(
                        model.Key,
                        model.IsActive,
                        model.ModName,
                        model.ModGroupId
                    );
                    model.LoadoutData = loadoutData;

                    return model;
                }
            );

        return result;
    }

    public override IReadOnlyList<FnvPluginReactiveSortItem> GetSortOrderItems(SortOrderId sortOrderId, IDb? db)
    {
        var dbToUse = db ?? Connection.Db;
        var sortOrder = LoadoutSortOrder.Load(dbToUse, sortOrderId);
        if (!sortOrder.IsValid())
            return [];

        var optionalCollection = sortOrder.ParentEntity.Match(
            loadoutId => DynamicData.Kernel.Optional<CollectionGroupId>.None,
            collectionGroupId => DynamicData.Kernel.Optional<CollectionGroupId>.Create(collectionGroupId)
        );

        var sortingData = RetrieveSortOrder(sortOrderId, dbToUse);
        var loadoutData = RetrieveLoadoutData(sortOrder.LoadoutId, optionalCollection, dbToUse);

        var groupPriorities = BuildGroupPriorityMap(sortOrder.LoadoutId, dbToUse);
        var reconciled = ReconcileWithPriorities(sortingData, loadoutData, groupPriorities);

        return reconciled.Select(tuple =>
            {
                return new FnvPluginReactiveSortItem(
                    tuple.SortedEntry.SortIndex,
                    tuple.SortedEntry.Key.Key,
                    tuple.ItemLoadoutData.ModName,
                    tuple.ItemLoadoutData.IsEnabled
                )
                {
                    ModGroupId = tuple.ItemLoadoutData.ModGroupId,
                    LoadoutData = tuple.ItemLoadoutData,
                };
            }
        ).ToList();
    }

    /// <summary>
    /// Returns the ordered list of plugin filenames for the given loadout.
    /// </summary>
    public IReadOnlyList<string> GetPluginOrder(LoadoutId loadoutId, Optional<CollectionGroupId> collectionGroupId, IDb? db = null)
    {
        var parentEntity = collectionGroupId.HasValue
            ? OneOf<LoadoutId, CollectionGroupId>.FromT1(collectionGroupId.Value)
            : OneOf<LoadoutId, CollectionGroupId>.FromT0(loadoutId);

        var sortOrderId = GetSortOrderIdFor(parentEntity, db);
        if (sortOrderId.HasValue)
        {
            return GetSortOrderItems(sortOrderId.Value, db)
                .Where(item => item.LoadoutData?.IsEnabled == true)
                .Select(item => item.Key.Key)
                .ToList();
        }

        var dbToUse = db ?? Connection.Db;
        var loadoutData = RetrieveLoadoutData(loadoutId, collectionGroupId, dbToUse);

        var groupPriorities = BuildGroupPriorityMap(loadoutId, dbToUse);
        var reconciled = ReconcileWithPriorities([], loadoutData, groupPriorities);

        var result = reconciled.Select(tuple =>
            {
                return new FnvPluginReactiveSortItem(
                    tuple.SortedEntry.SortIndex,
                    tuple.SortedEntry.Key.Key,
                    tuple.ItemLoadoutData.ModName,
                    tuple.ItemLoadoutData.IsEnabled
                )
                {
                    ModGroupId = tuple.ItemLoadoutData.ModGroupId,
                    LoadoutData = tuple.ItemLoadoutData,
                };
            }
        ).ToList();

        return result
            .Where(item => item.LoadoutData?.IsEnabled == true)
            .Select(item => item.Key.Key)
            .ToList();
    }

    protected override IReadOnlyList<(SortItemData<SortItemKey<string>> SortedEntry, SortItemLoadoutData<SortItemKey<string>> ItemLoadoutData)> ReconcileSortOrderCore(SortOrderId sortOrderId, IDb loadoutRevisionDb)
    {
        var sortOrder = LoadoutSortOrder.Load(loadoutRevisionDb.Connection.Db, sortOrderId);

        var collectionGroupId = sortOrder.ParentEntity.IsT1 ?
            sortOrder.ParentEntity.AsT1 :
            Optional<CollectionGroupId>.None;

        var loadoutData = RetrieveLoadoutData(sortOrder.LoadoutId, collectionGroupId, loadoutRevisionDb);
        var currentSortOrder = RetrieveSortOrder(sortOrderId, loadoutRevisionDb.Connection.Db);

        var groupPriorities = BuildGroupPriorityMap(sortOrder.LoadoutId, loadoutRevisionDb);
        return ReconcileWithPriorities(currentSortOrder, loadoutData, groupPriorities);
    }

    protected override void PersistSortOrderCore(
        SortOrderId sortOrderId,
        IReadOnlyList<SortItemData<SortItemKey<string>>> newOrder,
        ITransaction tx,
        IDb startingDb,
        CancellationToken token = default)
    {
        var persistentSortOrderEntries = startingDb.RetrieveFnvPluginSortableEntries(sortOrderId);

        token.ThrowIfCancellationRequested();

        // Remove outdated persistent items
        foreach (var dbItem in persistentSortOrderEntries)
        {
            var newItem = newOrder.FirstOrOptional(
                newItem => newItem.Key.Key == dbItem.PluginFileName
            );

            if (!newItem.HasValue)
            {
                tx.Delete(dbItem, recursive: false);
                continue;
            }

            var liveIdx = newOrder.IndexOf(newItem.Value);

            // Update existing items
            if (dbItem.AsSortOrderItem().SortIndex != liveIdx)
            {
                tx.Add(dbItem, SortOrderItem.SortIndex, liveIdx);
            }
        }

        // Add new items
        for (var i = 0; i < newOrder.Count; i++)
        {
            var newItem = newOrder[i];
            if (persistentSortOrderEntries.Any(si => si.PluginFileName == newItem.Key.Key))
                continue;

            var newDbItem = new SortOrderItem.New(tx)
            {
                ParentSortOrderId = sortOrderId,
                SortIndex = i,
            };

            _ = new FnvPluginSortOrderItem.New(tx, newDbItem)
            {
                SortOrderItem = newDbItem,
                PluginFileName = newItem.Key.Key,
            };
        }

        token.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    protected override IReadOnlyList<SortItemData<SortItemKey<string>>> RetrieveSortOrder(SortOrderId sortOrderEntityId, IDb dbToUse)
    {
        return FnvPluginExtensions.RetrieveFnvPluginSortOrderItems(dbToUse, sortOrderEntityId);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<SortItemLoadoutData<SortItemKey<string>>> RetrieveLoadoutData(LoadoutId loadoutId, DynamicData.Kernel.Optional<CollectionGroupId> collectionGroupId, IDb? db)
    {
        var dbToUse = db ?? Connection.Db;

        var result = FnvPluginExtensions.RetrieveWinningPluginsInLoadout(dbToUse, loadoutId)
            .Select(row => new SortItemLoadoutData<SortItemKey<string>>(
                new SortItemKey<string>(row.PluginFileName),
                row.IsEnabled,
                row.ModName,
                row.ModGroupId == 0 ? DynamicData.Kernel.Optional<LoadoutItemGroupId>.None : LoadoutItemGroupId.From(row.ModGroupId)
            ))
            .ToList();

        return result;
    }

    /// <summary>
    /// Builds a mapping from LoadoutItemGroupId to its conflict priority value.
    /// Groups with higher priority should load later (higher sort index) in FNV.
    /// </summary>
    private static Dictionary<LoadoutItemGroupId, ulong> BuildGroupPriorityMap(LoadoutId loadoutId, IDb db)
    {
        var priorities = LoadoutItemGroupPriority.FindByLoadout(db, loadoutId);
        var map = new Dictionary<LoadoutItemGroupId, ulong>();
        foreach (var priority in priorities)
        {
            map[LoadoutItemGroupId.From(priority.TargetId.Value)] = priority.Priority.Value;
        }
        return map;
    }

    /// <summary>
    /// Reconciles sort order with loadout data. When group priorities are available (e.g. from
    /// collection modRules), new items are sorted by their group's conflict priority rather
    /// than the default ESM-before-ESP heuristic.
    /// </summary>
    private IReadOnlyList<(SortItemData<SortItemKey<string>> SortedEntry, SortItemLoadoutData<SortItemKey<string>> ItemLoadoutData)> ReconcileWithPriorities(
        IReadOnlyList<SortItemData<SortItemKey<string>>> sourceSortedEntries,
        IReadOnlyList<SortItemLoadoutData<SortItemKey<string>>> loadoutDataItems,
        Dictionary<LoadoutItemGroupId, ulong> groupPriorities)
    {
        var loadoutItemsDict = loadoutDataItems.ToDictionary(item => item.Key);

        var results = new List<(SortItemData<SortItemKey<string>> SortedEntry, SortItemLoadoutData<SortItemKey<string>> ItemLoadoutData)>(sourceSortedEntries.Count);
        var processedKeys = new HashSet<SortItemKey<string>>(sourceSortedEntries.Count);

        foreach (var sortedEntry in sourceSortedEntries)
        {
            if (!loadoutItemsDict.TryGetValue(sortedEntry.Key, out var loadoutItemData))
                continue;

            processedKeys.Add(sortedEntry.Key);
            results.Add((sortedEntry, loadoutItemData));
        }

        // Add any remaining loadout items that were not in the source sorted entries
        var newItems = loadoutItemsDict.Values
            .Where(item => !processedKeys.Contains(item.Key));

        // If group priorities are available for any new items, use priority-based ordering.
        // This respects collection modRules (before/after) as encoded by ApplyCollectionDownloadRules.
        // Otherwise, fall back to ESM-before-ESP heuristic.
        var hasAnyPriority = groupPriorities.Count > 0;
        var itemsToAdd = newItems
            .Order(Comparer<SortItemLoadoutData<SortItemKey<string>>>.Create((a, b) =>
            {
                if (hasAnyPriority)
                {
                    var aPriority = a.ModGroupId.HasValue && groupPriorities.TryGetValue(a.ModGroupId.Value, out var ap) ? ap : ulong.MaxValue;
                    var bPriority = b.ModGroupId.HasValue && groupPriorities.TryGetValue(b.ModGroupId.Value, out var bp) ? bp : ulong.MaxValue;

                    if (aPriority != bPriority)
                        return aPriority.CompareTo(bPriority);
                }

                // ESM files sort before ESP files (ESMs should load first)
                var aIsEsm = a.Key.Key.EndsWith(".esm", StringComparison.OrdinalIgnoreCase);
                var bIsEsm = b.Key.Key.EndsWith(".esm", StringComparison.OrdinalIgnoreCase);

                if (aIsEsm != bIsEsm)
                    return aIsEsm ? -1 : 1;

                // Within same type/priority, sort by ModGroupId ascending (older items first), then by Key ascending
                return (a, b) switch
                {
                    ({ ModGroupId.HasValue: true }, { ModGroupId.HasValue: true }) =>
                        a.ModGroupId.Value.Value.CompareTo(b.ModGroupId.Value.Value) != 0
                            ? a.ModGroupId.Value.Value.CompareTo(b.ModGroupId.Value.Value)
                            : string.Compare(a.Key.Key, b.Key.Key, StringComparison.OrdinalIgnoreCase),
                    ({ ModGroupId.HasValue: true }, { ModGroupId.HasValue: false }) => -1,
                    ({ ModGroupId.HasValue: false }, { ModGroupId.HasValue: true }) => 1,
                    _ => string.Compare(a.Key.Key, b.Key.Key, StringComparison.OrdinalIgnoreCase),
                };
            }))
            .Select(loadoutItemData => (
                new SortItemData<SortItemKey<string>>(loadoutItemData.Key, 0),
                loadoutItemData
            ));

        // Append new items at the end (last loaded wins for FNV)
        results.AddRange(itemsToAdd);

        // Update sort indices
        for (var i = 0; i < results.Count; i++)
        {
            results[i].SortedEntry.SortIndex = i;
        }

        return results;
    }

    /// <inheritdoc />
    protected override IReadOnlyList<(SortItemData<SortItemKey<string>> SortedEntry, SortItemLoadoutData<SortItemKey<string>> ItemLoadoutData)> Reconcile(
        IReadOnlyList<SortItemData<SortItemKey<string>>> sourceSortedEntries,
        IReadOnlyList<SortItemLoadoutData<SortItemKey<string>>> loadoutDataItems)
    {
        // Delegate to priority-aware reconcile with empty priorities (fallback to ESM-before-ESP)
        return ReconcileWithPriorities(sourceSortedEntries, loadoutDataItems, new Dictionary<LoadoutItemGroupId, ulong>());
    }
}
