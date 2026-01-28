namespace Kata.Demo;

[BitLayout]
internal partial struct Test
{
    [BitField(8)]
    internal partial byte Test1 { get; set; }

    [BitField(7)]
    internal partial byte Test2 { get; set; }

    [BitField(1)]
    internal partial bool Test3 { get; set; }

    [BitField(16)]
    internal partial ushort Test4 { get; init; }
}

[BitLayout(BitOrder = BitOrder.MSBFirst)]
internal partial struct IPv4Start
{
    [BitField(4)] internal partial byte Version { get; set; }
    [BitField(4)] internal partial byte IHL { get; set; }
    [BitField(6)] internal partial byte DSCP { get; set; }
    [BitField(2)] internal partial byte ECN { get; set; }
    [BitField(16)] internal partial ushort TotalLength { get; set; }
}

[BitLayout]
internal partial struct BlobTest
{
    [BitField(32)] internal partial uint Field1 { get; set; }
    [BitField(32)] internal partial uint Field2 { get; set; }
    [BitField(16)] internal partial ushort Field3 { get; set; }
}

[BitLayout]
internal partial struct StraddlingBlob
{
    [BitField(60)] internal partial ulong Field1 { get; set; }
    [BitField(20)] internal partial int Field2 { get; set; }
}

[BitLayout(BitOrder = BitOrder.MSBFirst)]
internal partial struct MSBBlob
{
    [BitField(64)] internal partial ulong Field1 { get; set; }
    [BitField(64)] internal partial ulong Field2 { get; set; }
    [BitField(32)] internal partial uint Field3 { get; set; }
}

class Program
{
    static void Main()
    {
    }
}