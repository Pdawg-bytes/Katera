using Kata;

namespace Kata.Demo;

[BitLayout(Mode = StorageMode.Register)]
internal partial struct Test
{
    [BitField(8)]
    public partial byte Test1 { get; init; }

    [BitField(16)]
    public partial ushort Test2 { get; }
}

class Program
{
    static void Main()
    {
    }
}