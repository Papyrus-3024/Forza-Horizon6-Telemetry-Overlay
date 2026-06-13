using Fh6.Telemetry.Core;
using Xunit;

namespace Fh6.Telemetry.Tests;

public class PacketParserTests
{
    [Fact]
    public void SpanReader_reads_little_endian_sequentially()
    {
        // 0x01 as S32, then 2.0f as F32, then 0xAB as U8
        byte[] bytes = { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0xAB };
        var r = new SpanReader(bytes);

        Assert.Equal(1, r.S32());
        Assert.Equal(2.0f, r.F32());
        Assert.Equal(0xAB, r.U8());
        Assert.Equal(9, r.Position);
    }
}
