namespace Kata.Generator.Parsing;

internal sealed class PadModel(int bits) : LayoutItem
{
    internal int Bits { get; } = bits;
}