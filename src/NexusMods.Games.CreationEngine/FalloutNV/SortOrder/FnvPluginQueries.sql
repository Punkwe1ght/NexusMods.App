-- namespace: NexusMods.Games.CreationEngine.FalloutNV.SortOrder

CREATE SCHEMA IF NOT EXISTS fnvplugin;

-- Returns the FNV Plugin Sort Order Items for a given Sort Order Id
CREATE MACRO fnvplugin.FnvPluginSortOrderItems(db, sortOrderId) AS TABLE
SELECT s.PluginFileName, s.SortIndex, s.Id
FROM mdb_FnvPluginSortOrderItem(Db=>db) s
WHERE s.ParentSortOrder = sortOrderId
ORDER BY s.SortIndex;


-- Return loadout mod groups that contain a plugin file (esp/esm) under Data/ for a given loadout
CREATE MACRO fnvplugin.LoadoutPluginGroups(db, loadoutId, gameLocationId) AS TABLE
SELECT
    regexp_extract(file.TargetPath.Item3, '^Data\/([^\/]+\.(?:esp|esm))$', 1, 'i') AS PluginFileName,
    enabledState.IsEnabled AS IsEnabled,
    groupItem.Name AS ModName,
    groupItem.Id AS ModGroupId
FROM mdb_LoadoutItemWithTargetPath(Db=>db) as file
JOIN synchronizer.LeafLoadoutItems(db) as enabledState ON file.Id = enabledState.Id
JOIN mdb_LoadoutItemGroup(Db=>db) as groupItem ON file.Parent = groupItem.Id
WHERE file.TargetPath.Item1 = loadoutId
    AND file.TargetPath.Item2 = gameLocationId
    AND PluginFileName != '';


-- Return winning loadout plugin groups in case of multiple mods containing the same plugin
CREATE MACRO fnvplugin.WinningLoadoutPluginGroups(db, loadoutId, gameLocationId) AS TABLE
SELECT
    PluginFileName,
    IsEnabled,
    ModName,
    ModGroupId
FROM (
         SELECT
             matchingMods.*,
             ROW_NUMBER() OVER (
                            PARTITION BY matchingMods.PluginFileName
                            ORDER BY matchingMods.IsEnabled DESC, matchingMods.ModGroupId DESC
                        ) AS ranking
         FROM fnvplugin.LoadoutPluginGroups(db, loadoutId, gameLocationId) AS matchingMods
     ) ranked
WHERE ranking = 1
-- For FNV plugins, greater index wins (last loaded wins), so we put newest ModGroupId last
ORDER BY ModGroupId ASC, PluginFileName ASC;


-- Return the FNV Plugin Sort Order for a given loadout including the loadout data
CREATE MACRO fnvplugin.FnvPluginSortOrderWithLoadoutData(db, sortOrderId, loadoutId, gameLocationId) AS TABLE
SELECT
    sortItem.PluginFileName,
    sortItem.SortIndex,
    sortItem.Id,
    loadoutData.IsEnabled,
    loadoutData.ModName,
    loadoutData.ModGroupId
FROM mdb_FnvPluginSortOrderItem(Db=>db) sortItem
LEFT OUTER JOIN fnvplugin.WinningLoadoutPluginGroups(db, loadoutId, gameLocationId) loadoutData on sortItem.PluginFileName = loadoutData.PluginFileName
WHERE sortItem.ParentSortOrder = sortOrderId
ORDER BY sortItem.SortIndex;
