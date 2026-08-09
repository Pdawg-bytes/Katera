## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
KATERA001 | Katera.BitLayout | Error | Fields exceed declared size.
KATERA002 | Katera.BitLayout | Error | Property type cannot hold declared bit length.
KATERA003 | Katera.BitLayout | Error | Storage mode/size combination is unsupported.
KATERA004 | Katera.BitLayout | Error | Invalid BitField or Pad target usage.
KATERA005 | Katera.BitLayout | Error | Overlapping fields without overlap allowance.
KATERA006 | Katera.BitLayout | Warning | Implicit gap, use Pad to make it explicit.
KATERA007 | Katera.BitLayout | Error | BitField length must be greater than zero.