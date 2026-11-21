# Numeric Conversions, Rounding and Overflow (Solution)

Official solution for the `numeric-conversions-rounding-and-overflow` lab.

## What changed from the starter

- `Math.Clamp` before the cast avoids the unspecified result of an out-of-range `(byte)` cast; NaN is checked first because it compares false against everything.
- `Math.Round` defaults to `MidpointRounding.ToEven` (2.345 would not become 2.35 anyway as a double, but a `decimal` makes the rule visible). `AwayFromZero` is the usual accounting rule.
- `checked((int)Math.Truncate(value))` turns NaN, infinities and out-of-range values into `OverflowException`. Without `checked` the same cast returns an unspecified value.
- `Convert.ToInt32(2.5)` is 2 and `Convert.ToInt32(3.5)` is 4 (half to even); the cast `(int)2.5` truncates to 2 and `(int)3.5` to 3.
- `checked` applies to the arithmetic written inside its expression or block; `total = checked(total + value)` covers the addition per element.
- `TryAddExact` widens to `long` so no exception is needed for the expected failure.
- TODO-09 (needs the .NET 11 SDK; with SDK 10 the lab builds only for `net10.0` and it does not appear): `Math.Round(double, digits)` and `(decimal)` of a `double` now work from the exact binary value. `Math.Round(2.675, 2)` is 2.68 on .NET 10 and 2.67 on .NET 11, `Math.Round(1.15, 1)` goes from 1.2 to 1.1, `(decimal)(0.1 + 0.2)` goes from 0.3 to 0.3000000000000000444089209850, and `Math.Round(1.5, 16)` stopped throwing. The `Net11ChangesTests` pin the .NET 11 values; the `decimal` overloads of `Math.Round` did not change.
- Bonus: `CreateChecked` throws like a `checked` cast, `CreateSaturating` clamps, and `SplitEvenly` gives the remainder to the last share so the parts always sum to the total.

## Running

```bash
dotnet test
```
