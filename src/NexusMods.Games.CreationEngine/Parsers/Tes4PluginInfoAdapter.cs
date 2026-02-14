using Mutagen.Bethesda.Plugins;
using NexusMods.Games.CreationEngine.Abstractions;

namespace NexusMods.Games.CreationEngine.Parsers;

/// <summary>
/// Adapts a Tes4PluginHeader to IPluginInfo.
/// </summary>
public sealed class Tes4PluginInfoAdapter : IPluginInfo
{
    public ModKey ModKey { get; }
    public IReadOnlyList<ModKey> Masters { get; }

    public Tes4PluginInfoAdapter(string fileName, Tes4PluginHeader header)
    {
        ModKey = ModKey.FromFileName(fileName);
        Masters = header.MasterReferences
            .Select(m => ModKey.FromFileName(m))
            .ToList();
    }
}
