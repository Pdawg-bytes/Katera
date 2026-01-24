using Kata;

namespace Kata.Demo;

[BitLayout]
internal partial struct Test
{
    [BitField(8)]
    public partial byte Test1 { get; set; }

    [BitField(7)]
    public partial byte Test2 { get; set; }

    [BitField(1)]
    public partial bool Test3 { get; set; }

    [BitField(16)]
    public partial ushort Test4 { get; set; }
}


class Program
{
    static void Main()
    {
    }
}