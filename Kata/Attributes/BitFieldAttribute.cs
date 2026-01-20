using System;

namespace Kata;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class BitFieldAttribute(int length) : Attribute
{
    public int Length    { get; }      = length;
    public int Offset    { get; set; } = -1;
    public bool IsSigned { get; set; } = false;
}