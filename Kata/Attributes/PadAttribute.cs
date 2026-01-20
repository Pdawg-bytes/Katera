using System;

namespace Kata;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class PadAttribute(int bits) : Attribute
{
    public int Bits { get; } = bits;
}