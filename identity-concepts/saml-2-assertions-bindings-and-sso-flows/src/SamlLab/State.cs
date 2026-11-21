using System.Collections.Concurrent;

namespace SamlLab;

/// <summary>AuthnRequest IDs this SP issued and has not yet seen answered (the SP-initiated flow).</summary>
public sealed class PendingRequests
{
    private readonly ConcurrentDictionary<string, byte> _ids = new();

    public void Add(string id) => _ids[id] = 0;

    /// <summary>True exactly once per issued ID.</summary>
    public bool TryConsume(string id) => _ids.TryRemove(id, out _);

    public int Count => _ids.Count;
}

/// <summary>Remembers the assertion IDs already accepted, so a captured response cannot be posted twice.</summary>
public sealed class ReplayCache(TimeProvider time)
{
    private readonly Dictionary<string, DateTimeOffset> _seen = new();
    private readonly object _gate = new();

    public int Count { get { lock (_gate) return _seen.Count; } }

    // TODO-06: Register an assertion ID. Return false when it was already registered and is still
    // remembered. Remember an ID only until the assertion itself would expire (notOnOrAfter), and
    // forget the expired entries so the cache cannot grow forever.
    public bool TryRegister(string assertionId, DateTimeOffset notOnOrAfter)
    {
        throw new NotImplementedException();
    }
}
