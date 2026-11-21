#if NET11_0_OR_GREATER
namespace LinqLab.Tests;

public class FullJoinTests
{
    private static readonly Customer Ana = new(1, "Ana", "ana@x.com");
    private static readonly Customer Bia = new(2, "Bia", "bia@x.com");
    private static readonly Customer Caio = new(3, "Caio", "caio@x.com");

    private static readonly Order[] Orders =
    [
        new(10, 1, 100m),
        new(11, 1, 50m),
        new(12, 2, 30m),
        new(13, null, 999m),
    ];

    [Fact]
    public void ShouldKeepTheUnmatchedRowsOfBothSides()
    {
        var rows = Queries.ReconcileOrders([Ana, Bia, Caio], Orders);

        Assert.Equal(
            [("Ana", (int?)10), ("Ana", 11), ("Bia", 12), ("Caio", null), (null, 13)],
            rows);
    }

    [Fact]
    public void ShouldNotMatchTwoOrdersThatBothHaveNoCustomer()
    {
        Order[] orphans = [new(20, null, 1m), new(21, null, 2m)];

        var rows = Queries.ReconcileOrders([Ana], orphans);

        Assert.Equal([("Ana", (int?)null), (null, 20), (null, 21)], rows);
    }

    [Fact]
    public void ShouldPairByEmailIgnoringCaseAndLeaveTheMissingSideNull()
    {
        var anaAgain = new Customer(9, "Ana again", "ANA@X.COM");
        var dora = new Customer(10, "Dora", "dora@x.com");

        var rows = Queries.ReconcileByEmail([Ana, Bia], [anaAgain, dora]);

        Assert.Equal([(Ana, anaAgain), (Bia, null), (null, dora)], rows);
    }

    [Fact]
    public void ShouldReturnEveryRowWhenNothingMatches()
    {
        var rows = Queries.ReconcileByEmail([Ana], [Bia]);

        Assert.Equal([(Ana, null), (null, Bia)], rows);
    }
}
#endif
