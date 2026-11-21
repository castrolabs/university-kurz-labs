using System.ComponentModel;
using ModelContextProtocol.Server;

namespace StockMcp;

[McpServerPromptType]
public sealed class StockPrompts
{
    [McpServerPrompt(Name = "restock_plan"), Description("A restock plan request for one SKU.")]
    public static string RestockPlan([Description("The SKU to restock")] string sku) =>
        $"Write a three-step restock plan for {sku}.";
}

[McpServerResourceType]
public sealed class StockResources
{
    [McpServerResource(UriTemplate = "stock://policy", Name = "reservation_policy", MimeType = "text/plain"),
     Description("How long a reservation is kept.")]
    public static string ReservationPolicy() => "Reservations expire after 24 hours.";
}
