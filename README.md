# Katera
Katera is a source generator for deterministic bitfield layouts in C#. Its goal is to add support for bitfield-style structs without inheriting the undefined or host-dependent behavior of C/C++ bitfields.

All bitfield layouts support LSB-first and MSB-first ordering, explicit padding, overlapping fields, and memory-backed overlays for interpreting raw bytes.

## Features
- Partial `struct` source generation for compact bit layouts
- Support for `bool`, integer types, and enums as bit fields
- Explicit layout control with `Size`, `Mode`, `AllowOverlap`, `BitOrder`, and `Offset`
- Automatic layout size inference when `Size` is omitted
- Padding with `PadAttribute`
- Support for get-only and init-only bit properties
- Blob-backed and register-backed storage modes with optional overlay views
- Analyzer diagnostics for invalid field usage, size mismatches, overlaps, and gaps

## Usage
Katera generates bitfield logic for `partial struct` types annotated with `BitLayoutAttribute`. Declare each bit field property as `partial` with `BitFieldAttribute` and optionally use `PadAttribute` or `Offset` to control exact placement.

```csharp
using Katera;

[BitLayout]
public partial struct PacketHeader
{
    [BitField(3)]  public partial byte   Version  { get; init; }
    [BitField(5)]  public partial byte   Flags    { get; set; }
    [BitField(16)] public partial ushort Length   { get; set; }
    [BitField(8)]  public partial byte   Checksum { get; set; }
}
```

Katera will generate the backing storage and property accessors so you can treat `PacketHeader` like a compact value type.

## BitLayoutAttribute
Apply `BitLayoutAttribute` to a `partial struct` to configure the layout.

Properties:
- `Size` (bits): total layout size. `0` (default) means automatically compute size from fields.
- `Mode`: one of `StorageMode.Auto` (default), `Register`, `Blob`, or `Expanded`.
- `AllowOverlap`: set to `true` to allow overlapping fields (default `false`).
- `BitOrder`: choose `BitOrder.LSBFirst` (default) or `BitOrder.MSBFirst`.

Example:

```csharp
[BitLayout(Size = 32, Mode = StorageMode.Register, BitOrder = BitOrder.MSBFirst)]
public partial struct Flags32
{
    [BitField(1)]  public partial bool Enabled { get; set; }
    [BitField(7)]  public partial byte Type    { get; set; }
    [BitField(24)] public partial uint Value   { get; set; }
}
```

## BitFieldAttribute
Use `BitFieldAttribute(length)` to declare a field's bit width. Supported bit field types are:
- `bool`
- `byte`, `sbyte`
- `short`, `ushort`
- `int`, `uint`
- `long`, `ulong`
- enums (backed by a supported integral type)

Example with explicit offset:

```csharp
[BitLayout]
public partial struct ExplicitOffsets
{
    [BitField(4, Offset = 0)] public partial byte A { get; set; }
    [BitField(4, Offset = 4)] public partial byte B { get; set; }
    [BitField(8, Offset = 8)] public partial byte C { get; set; }
}
```

## Padding
Use `PadAttribute` to reserve unnamed bits between fields.

```csharp
[BitLayout]
public partial struct PaddedLayout
{
    [BitField(5)] public partial byte A { get; set; }
    [Pad(3)]
    [BitField(8)] public partial byte B { get; set; }
}
```

This is useful when you want explicit gaps or alignment without creating a named field. If gaps are created implicitly, the analyzer will emit a `KATERA006` warning, so it is recommended to use this attribute.

## Read-only and init-only properties
Katera supports read-only bit fields and `init`-only bit fields.

```csharp
[BitLayout]
public partial struct ReadOnlyLayout
{
    [BitField(8)] public partial byte ReadOnlyValue { get; }
    [BitField(8)] public partial byte InitValue     { get; init; }
}
```

The generator omits setters for get-only fields and generates `init` setters when the property is declared with `init`.

## Enums and bools
Enums are supported as long as their underlying type is one of the supported integral types.

```csharp
public enum Color : byte { Red, Green, Blue }

[BitLayout]
public partial struct EnumLayout
{
    [BitField(2)] public partial Color Color { get; set; }
    [BitField(1)] public partial bool IsVisible { get; set; }
}
```

`bool` fields are stored as a single bit and will not allow larger sizes.

## Straddling fields
Fields may cross underlying storage boundaries in blob-backed layouts. Katera handles reads and writes across 64-bit boundaries automatically.

```csharp
[BitLayout(Mode = StorageMode.Blob)]
public partial struct WideLayout
{
    [BitField(62)] public partial ulong First { get; set; }
    [BitField(4)]  public partial uint Second { get; set; }
}
```

## Storage modes
`StorageMode.Auto` is the default. It chooses `Register` for layouts up to 8 bytes and `Blob` for larger layouts.

- `StorageMode.Register`
  - Uses a single primitive backing storage value (`byte`, `ushort`, `uint`, or `ulong`)
  - Valid only for layouts of 8 bytes or smaller
- `StorageMode.Blob`
  - Uses one or more `ulong` values internally
  - Valid only for layouts larger than 8 bytes
- `StorageMode.Expanded`
  - Uses separate generated fields and properties without unified backing storage

## Overlapping fields
Set `AllowOverlap = true` to permit overlapping fields. Overlap is not allowed by default.

```csharp
[BitLayout(AllowOverlap = true)]
public partial struct OverlapLayout
{
    [BitField(8)]             
    public partial byte A { get; set; }

    [BitField(8, Offset = 0)] 
    public partial byte AliasOfA { get; set; }
}
```

## Overlay views
For blob-backed layouts, Katera can generate an overlay view type named `{TypeName}View`.

```csharp
[BitLayout(Mode = StorageMode.Blob)]
public partial struct Packet
{
    [BitField(16)] public partial ushort ID       { get; set; }
    [BitField(16)] public partial ushort Checksum { get; set; }
}
```

The generated overlay type exposes:
- `static PacketView Over(Span<byte> span)`
- `static PacketView Over(ref byte reference)`
- `Span<byte> AsSpan()`

Overlay helpers require a buffer that contains at least the full layout size in bytes, which is the declared `Size` in bits rounded up to the next whole byte. The returned view covers exactly that many bytes.

Use overlays when you need to interpret or mutate a raw byte buffer without copying the entire bitfield struct.

## Reading and writing raw bytes
Generated types offer helpers for raw byte conversion.

Raw helpers such as `From(ReadOnlySpan<byte>)`, `TryFrom(ReadOnlySpan<byte>, out T)`, and `WriteTo(Span<byte>)` all require a buffer at least as large as the layout's full backing size in bytes. That is:
- the declared `Size` in bits, rounded up to the nearest whole byte
- or the computed size when `Size` is omitted

For `Register`-backed layouts, this is the same size as the generated backing primitive (`byte`, `ushort`, `uint`, or `ulong`).
For `Blob`-backed layouts, this is the full number of bytes covered by the generated blob storage.

Example:

```csharp
var data   = new byte[4];
var header = PacketHeader.From(data);

header.Flags = 3;
header.WriteTo(data);

if (PacketHeader.TryFrom(data, out var parsed))
{
    Console.WriteLine(parsed.Length);
}
```

## Diagnostics and errors
Katera reports analyzer diagnostics for invalid layouts.

Rule ID | Category | Severity | Notes
--------|----------|----------|------
KATERA001 | Katera.BitLayout | Error | Fields exceed declared size.
KATERA002 | Katera.BitLayout | Error | Property type cannot hold declared bit length.
KATERA003 | Katera.BitLayout | Error | Storage mode/size combination is unsupported.
KATERA004 | Katera.BitLayout | Error | Invalid BitField or Pad target usage.
KATERA005 | Katera.BitLayout | Error | Overlapping fields without overlap allowance.
KATERA006 | Katera.BitLayout | Warning | Implicit gap, use Pad to make it explicit.
KATERA007 | Katera.BitLayout | Error | BitField length must be greater than zero.

## Notes
- `BitLayoutAttribute` must be applied to a `partial struct`.
- Bit field properties must be instance properties and must be `partial`.
- `PadAttribute` may be applied to structs, fields, or properties.
- Explicit offsets and `Pad` may be used together to control layout precisely.
- `Size` is in bits; if omitted, Katera computes the smallest byte size necessary for the declared fields.