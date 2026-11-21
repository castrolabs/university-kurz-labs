using System.ComponentModel;
using ModelContextProtocol.Server;

namespace StockMcp;

public sealed class StockPrompts
{
    public static string RestockPlan(string sku)
    {
        // TODO-03: Expose a prompt named "restock_plan" (with descriptions) that returns
        //          "Write a three-step restock plan for <sku>.", and on StockResources below a resource at
        //          the URI "stock://policy" that returns "Reservations expire after 24 hours."
        throw new NotImplementedException();
    }
}

public sealed class StockResources
{
    public static string ReservationPolicy() => throw new NotImplementedException();
}
