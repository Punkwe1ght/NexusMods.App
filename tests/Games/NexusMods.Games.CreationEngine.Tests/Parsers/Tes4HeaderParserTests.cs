using NexusMods.Games.CreationEngine.Parsers;

namespace NexusMods.Games.CreationEngine.Tests.Parsers;

public class Tes4HeaderParserTests
{
    [Fact]
    public void Parse_ValidTes4WithMasters_ReturnsMasterList()
    {
        // Build a minimal TES4 record with two MAST subrecords
        using var stream = BuildTes4Stream(
            flags: 0x01, // ESM
            masters: ["FalloutNV.esm", "DeadMoney.esm"]);

        var header = Tes4HeaderParser.Parse(stream);

        Assert.NotNull(header);
        Assert.True(header!.IsEsm);
        Assert.Equal(2, header.MasterReferences.Count);
        Assert.Equal("FalloutNV.esm", header.MasterReferences[0]);
        Assert.Equal("DeadMoney.esm", header.MasterReferences[1]);
    }

    [Fact]
    public void Parse_EspWithNoMasters_ReturnsEmptyList()
    {
        using var stream = BuildTes4Stream(flags: 0x00, masters: []);

        var header = Tes4HeaderParser.Parse(stream);

        Assert.NotNull(header);
        Assert.False(header!.IsEsm);
        Assert.Empty(header.MasterReferences);
    }

    [Fact]
    public void Parse_InvalidSignature_ReturnsNull()
    {
        using var stream = new MemoryStream([0x00, 0x00, 0x00, 0x00]);

        var header = Tes4HeaderParser.Parse(stream);

        Assert.Null(header);
    }

    /// <summary>
    /// Builds a minimal binary TES4 record for testing.
    /// </summary>
    private static MemoryStream BuildTes4Stream(uint flags, string[] masters)
    {
        using var inner = new MemoryStream();
        using var writer = new BinaryWriter(inner, System.Text.Encoding.ASCII, leaveOpen: true);

        // Build subrecord block first to calculate dataSize
        using var subStream = new MemoryStream();
        using var subWriter = new BinaryWriter(subStream, System.Text.Encoding.ASCII, leaveOpen: true);

        // Write a dummy HEDR subrecord (required, but parser skips it)
        subWriter.Write("HEDR"u8);
        subWriter.Write((ushort)12); // size
        subWriter.Write(1.34f);      // version
        subWriter.Write(0);          // numRecords
        subWriter.Write(0);          // nextObjectId

        foreach (var master in masters)
        {
            // MAST subrecord
            var nameBytes = System.Text.Encoding.ASCII.GetBytes(master + '\0');
            subWriter.Write("MAST"u8);
            subWriter.Write((ushort)nameBytes.Length);
            subWriter.Write(nameBytes);

            // DATA subrecord (always 8 bytes of zeros)
            subWriter.Write("DATA"u8);
            subWriter.Write((ushort)8);
            subWriter.Write(0L);
        }
        subWriter.Flush();
        var subData = subStream.ToArray();

        // Write TES4 record header (20 bytes)
        writer.Write("TES4"u8);
        writer.Write((uint)subData.Length); // dataSize
        writer.Write(flags);
        writer.Write(0u); // formId
        writer.Write(0u); // vcInfo

        // Write subrecord data
        writer.Write(subData);
        writer.Flush();

        return new MemoryStream(inner.ToArray());
    }
}
