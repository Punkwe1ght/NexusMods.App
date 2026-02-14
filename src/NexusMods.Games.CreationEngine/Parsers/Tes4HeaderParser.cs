using System.Text;

namespace NexusMods.Games.CreationEngine.Parsers;

/// <summary>
/// Reads TES4 record headers from Gamebryo-era plugin files (FNV, FO3, Oblivion).
/// Record layout: type[4] + dataSize[4] + flags[4] + formId[4] + vcInfo[4] = 20 bytes.
/// Each MAST subrecord contains a null-terminated filename string followed by a DATA subrecord (8 bytes).
/// </summary>
public static class Tes4HeaderParser
{
    private const int Tes4HeaderSize = 20;
    private static readonly byte[] Tes4Signature = "TES4"u8.ToArray();
    private static readonly byte[] MastSignature = "MAST"u8.ToArray();

    /// <summary>
    /// Parse the TES4 record header from a stream positioned at byte 0.
    /// Returns null if the stream does not contain a valid TES4 record.
    /// </summary>
    public static Tes4PluginHeader? Parse(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        // Read record header (20 bytes for Gamebryo-era plugins)
        var type = reader.ReadBytes(4);
        if (!type.AsSpan().SequenceEqual(Tes4Signature))
            return null;

        var dataSize = reader.ReadUInt32();
        var flags = reader.ReadUInt32();
        _ = reader.ReadUInt32(); // formId (always 0 for TES4)
        _ = reader.ReadUInt32(); // vcInfo

        // Read subrecords within the TES4 data block
        var masters = new List<string>();
        var endPos = stream.Position + dataSize;

        while (stream.Position < endPos)
        {
            if (endPos - stream.Position < 6) // minimum subrecord: type[4] + size[2]
                break;

            var subType = reader.ReadBytes(4);
            var subSize = reader.ReadUInt16();

            if (subType.AsSpan().SequenceEqual(MastSignature))
            {
                var nameBytes = reader.ReadBytes(subSize);
                // Strip null terminator
                var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                if (!string.IsNullOrEmpty(name))
                    masters.Add(name);
            }
            else
            {
                // Skip unknown subrecord
                if (subSize > 0)
                    stream.Seek(subSize, SeekOrigin.Current);
            }
        }

        return new Tes4PluginHeader
        {
            MasterReferences = masters,
            Flags = flags,
        };
    }
}
