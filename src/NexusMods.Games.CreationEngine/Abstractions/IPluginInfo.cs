using Mutagen.Bethesda.Plugins;

namespace NexusMods.Games.CreationEngine.Abstractions;

/// <summary>
/// Minimal plugin metadata needed for load ordering and master validation.
/// Implemented by both Mutagen IMod (via MutagenPluginInfoAdapter) and Tes4PluginHeader (via Tes4PluginInfoAdapter).
/// </summary>
public interface IPluginInfo
{
    ModKey ModKey { get; }
    IReadOnlyList<ModKey> Masters { get; }
}
