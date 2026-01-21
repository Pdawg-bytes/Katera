using Kata;

namespace Kata.Demo;

[BitLayout]
internal partial struct Test
{
    [BitField(8)]
    public partial byte Test1 { get; set; }

    [BitField(1)]
    public partial bool Test2 { get; set; }

    [BitField(7)]
    public partial byte Test3 { get; set; }

    [BitField(8)]
    public partial TestEnum Test4 { get; set; }

    // TODO: Fix warnings about this even when Pad is present
    [Pad(4)]

    [BitField(1, Offset = 28)]
    public partial bool Test5 { get; set; }
}

internal enum TestEnum
{
    None,
}

class Program
{
    static void Main()
    {
    }
}