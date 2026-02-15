using JetBrains.Annotations;
using NexusMods.MnemonicDB.Abstractions.Attributes;
using NexusMods.MnemonicDB.Abstractions.Models;
using LoadoutSortOrder = NexusMods.Abstractions.Loadouts.SortOrder;

namespace NexusMods.Games.CreationEngine.FalloutNV.SortOrder;

/// <summary>
/// Represents the FNV plugin load order.
/// </summary>
[PublicAPI]
[Include<LoadoutSortOrder>]
public partial class FnvPluginSortOrder : IModelDefinition
{
    private const string Namespace = "NexusMods.Games.CreationEngine.FalloutNV.FnvPluginSortOrder";

    /// <summary>
    /// Marker attribute for querying the model.
    /// Needs to be explicitly set to true on new model creation.
    /// </summary>
    public static readonly MarkerAttribute Marker = new(Namespace, nameof(Marker));
}
