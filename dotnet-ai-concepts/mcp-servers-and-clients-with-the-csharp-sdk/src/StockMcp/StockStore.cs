using System.Collections.Concurrent;

namespace StockMcp;

/// <summary>In-memory stock. One instance per server, registered as a singleton.</summary>
public sealed class StockStore
{
    private readonly ConcurrentDictionary<string, int> _units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SKU-1"] = 12,
        ["SKU-2"] = 0,
    };

    private readonly ConcurrentQueue<string> _reservations = new();

    /// <summary>Every reservation made, as "quantity x sku". The tests use it to see what reached the server.</summary>
    public IReadOnlyCollection<string> Reservations => _reservations;

    public int? Find(string sku) => _units.TryGetValue(sku, out var units) ? units : null;

    public void Reserve(string sku, int quantity) => _reservations.Enqueue($"{quantity} x {sku}");
}
