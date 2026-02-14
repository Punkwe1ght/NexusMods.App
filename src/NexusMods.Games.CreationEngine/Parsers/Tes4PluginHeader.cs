namespace NexusMods.Games.CreationEngine.Parsers;

/// <summary>
/// Parsed TES4 record header from a Gamebryo/CreationEngine plugin file.
/// Contains only the fields needed for load ordering and master validation.
/// </summary>
public sealed record Tes4PluginHeader
{
    /// <summary>
    /// Ordered list of master file dependencies (.esm/.esp filenames).
    /// </summary>
    public required IReadOnlyList<string> MasterReferences { get; init; }

    /// <summary>
    /// Raw record flags from the TES4 header. Bit 0 indicates ESM.
    /// </summary>
    public required uint Flags { get; init; }

    /// <summary>
    /// True if the ESM flag (bit 0) is set.
    /// </summary>
    public bool IsEsm => (Flags & 0x1) != 0;
}
