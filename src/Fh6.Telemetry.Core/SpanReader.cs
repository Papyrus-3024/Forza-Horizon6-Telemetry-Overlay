using System.Buffers.Binary;

namespace Fh6.Telemetry.Core;

internal ref struct SpanReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _pos;

    public SpanReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _pos = 0;
    }

    public int Position => _pos;

    public float F32()
    {
        var v = BinaryPrimitives.ReadSingleLittleEndian(_data.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public int S32()
    {
        var v = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public uint U32()
    {
        var v = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_pos, 4));
        _pos += 4;
        return v;
    }

    public ushort U16()
    {
        var v = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_pos, 2));
        _pos += 2;
        return v;
    }

    public byte U8() => _data[_pos++];

    public sbyte S8() => (sbyte)_data[_pos++];
}
