namespace AuthzLab;

public sealed partial class RelationStore
{
    private bool Check(string user, string relation, string obj, HashSet<string> visiting)
    {
        if (!model.TryGetValue((TypeOf(obj), relation), out var rewrite)) return false;

        var key = $"{user}|{relation}|{obj}";
        if (!visiting.Add(key)) return false;
        try
        {
            return Evaluate(rewrite, user, relation, obj, visiting);
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    private bool Evaluate(Rewrite rewrite, string user, string relation, string obj, HashSet<string> visiting) => rewrite switch
    {
        This => Direct(user, relation, obj, visiting),
        ComputedUserset c => Check(user, c.Relation, obj, visiting),
        TupleToUserset t => _tuples
            .Where(x => x.Object == obj && x.Relation == t.Tupleset && !x.User.Contains('#'))
            .Any(x => Check(user, t.Relation, x.User, visiting)),
        Union u => u.Children.Any(child => Evaluate(child, user, relation, obj, visiting)),
        Intersection i => i.Children.All(child => Evaluate(child, user, relation, obj, visiting)),
        _ => throw new NotSupportedException(rewrite.GetType().Name),
    };

    private bool Direct(string user, string relation, string obj, HashSet<string> visiting)
    {
        foreach (var tuple in _tuples.Where(t => t.Object == obj && t.Relation == relation))
        {
            if (tuple.User == user) return true;
            if (tuple.User == "user:*" && user.StartsWith("user:", StringComparison.Ordinal)) return true;

            var hash = tuple.User.IndexOf('#');
            if (hash > 0 && Check(user, tuple.User[(hash + 1)..], tuple.User[..hash], visiting)) return true;
        }
        return false;
    }

    /// <summary>"What can this user see?" The question a document list page needs.</summary>
    public IReadOnlyList<string> ListObjects(string user, string relation, string type) =>
        [.. _tuples.Select(t => t.Object)
            .Where(o => o.StartsWith(type + ":", StringComparison.Ordinal))
            .Distinct()
            .Where(o => Check(user, relation, o))
            .Order(StringComparer.Ordinal)];
}
