using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace StructuredOutputLab;

public enum Priority { Low, Normal, Urgent }

public enum Category { Billing, Shipping, Technical }

public record Address(string City, string Country);

[Description("A support ticket extracted from a customer email")]
public record Ticket(
    [property: Description("Customer full name")] string Customer,
    int Quantity,
    Priority Priority,
    string? OrderNumber,
    List<string> Products,
    Address ShipTo,
    DateOnly OrderDate);

public record Order
{
    public required string OrderNumber { get; init; }

    [Range(1, 100)]
    public int Quantity { get; init; }
}

public record Sentiment(string Label, int Score);

/// <summary>What a parse produced: the value, or the exception type and message that stopped it.</summary>
public sealed record ParseOutcome<T>(T? Value, string? Error)
{
    public bool Ok => Error is null;
}

public sealed record RetryOutcome<T>(T? Value, int Attempts);
