using NexusMods.Abstractions.Loadouts;
using NexusMods.MnemonicDB.Abstractions.Attributes;
using NexusMods.MnemonicDB.Abstractions.Models;

namespace NexusMods.Games.CreationEngine.FalloutNV.Models;

/// <summary>
/// Marks a loadout item group as containing an xNVSE plugin.
/// </summary>
[Include<LoadoutItemGroup>]
public partial class NvsePluginLoadoutItem : IModelDefinition
{
    private const string Namespace = "NexusMods.CreationEngine.FalloutNV.NvsePluginLoadoutItem";

    /// <summary>
    /// Marker indicating this group contains an xNVSE plugin.
    /// </summary>
    public static readonly MarkerAttribute NvsePlugin =
        new(Namespace, nameof(NvsePlugin)) { IsIndexed = true };

    /// <summary>
    /// Optional version string for the xNVSE plugin.
    /// </summary>
    public static readonly StringAttribute PluginVersion =
        new(Namespace, nameof(PluginVersion)) { IsOptional = true };
}
