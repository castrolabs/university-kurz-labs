#if NET11_0_OR_GREATER
using NumericLab;

namespace NumericLab.Tests;

// These tests pin the .NET 11 results. On net10.0 the same calls give 2.68, 1.2 and 0.3, and
// Math.Round(1.5, 16) throws, which is why this file only compiles for net11.0.
public class Net11ChangesTests
{
    [Fact]
    public void ShouldRoundTheExactValueOfTheDouble() =>
        Assert.Equal(2.67, Conversions.RoundDouble(2.675, 2));

    [Fact]
    public void ShouldRoundOnePointFifteenDown() =>
        Assert.Equal(1.1, Conversions.RoundDouble(1.15, 1));

    [Fact]
    public void ShouldKeepTheExactDoubleWhenConvertingToDecimal() =>
        Assert.Equal(0.3000000000000000444089209850m, Conversions.DoubleToDecimal(0.1 + 0.2));

    [Fact]
    public void ShouldAcceptMoreThanFifteenDigits() =>
        Assert.Equal(1.5, Conversions.RoundDouble(1.5, 16));

    [Fact]
    public void ShouldAgreeWithRoundingTheExactDecimalExpansion()
    {
        foreach (var (value, digits) in new[] { (2.675, 2), (1.15, 1), (0.125, 2), (1.015, 2) })
        {
            Assert.Equal(Conversions.RoundViaExactDecimal(value, digits), Conversions.RoundDouble(value, digits));
        }
    }
}
#endif
