using JetBrains.Annotations;
using NexusMods.Abstractions.Games;
using NexusMods.MnemonicDB.Abstractions.Attributes;
using NexusMods.MnemonicDB.Abstractions.Models;

namespace NexusMods.Games.CreationEngine.FalloutNV.SortOrder;

/// <summary>
/// Represents a single plugin entry in the FNV plugin sort order.
/// </summary>
[PublicAPI]
[Include<SortOrderItem>]
public partial class FnvPluginSortOrderItem : IModelDefinition
{
    private const string Namespace = "NexusMods.Games.CreationEngine.FalloutNV.FnvPluginSortOrderItem";

    /// <summary>
    /// The plugin filename (e.g., "YUP - Base.esm").
    /// </summary>
    public static readonly StringAttribute PluginFileName = new(Namespace, nameof(PluginFileName));
}
