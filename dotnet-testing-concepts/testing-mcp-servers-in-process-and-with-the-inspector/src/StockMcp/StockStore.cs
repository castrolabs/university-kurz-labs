namespace StockMcp;

public sealed class StockStore
{
    private readonly Dictionary<string, int> _units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SKU-1"] = 12,
        ["SKU-2"] = 0,
    };

    public int? Find(string sku) => _units.TryGetValue(sku, out var units) ? units : null;
}
