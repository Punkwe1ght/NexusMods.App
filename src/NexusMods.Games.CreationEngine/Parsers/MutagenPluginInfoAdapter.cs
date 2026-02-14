using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using NexusMods.Games.CreationEngine.Abstractions;

namespace NexusMods.Games.CreationEngine.Parsers;

/// <summary>
/// Adapts a Mutagen IMod to IPluginInfo.
/// </summary>
public sealed class MutagenPluginInfoAdapter : IPluginInfo
{
    public ModKey ModKey { get; }
    public IReadOnlyList<ModKey> Masters { get; }

    public MutagenPluginInfoAdapter(IMod mod)
    {
        ModKey = mod.ModKey;
        Masters = mod.MasterReferences
            .Select(m => m.Master)
            .ToList();
    }
}
