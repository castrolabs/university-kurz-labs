namespace AuthzLab;

/// <summary>`object#relation@user`: "user:bob" is a viewer of "folder:f-eng". The user side may also be a userset ("team:eng#member") or the wildcard "user:*".</summary>
public sealed record RelationTuple(string Object, string Relation, string User);

/// <summary>How a relation is computed (the Zanzibar "userset rewrite", the DSL of OpenFGA).</summary>
public abstract record Rewrite
{
    public static Rewrite Or(params Rewrite[] children) => new Union(children);
    public static Rewrite And(params Rewrite[] children) => new Intersection(children);
    public static Rewrite Direct() => new This();
    public static Rewrite Computed(string relation) => new ComputedUserset(relation);
    public static Rewrite FromParent(string tupleset, string relation) => new TupleToUserset(tupleset, relation);
}

/// <summary>`[user, team#member]`: whoever has a tuple for this relation on this object.</summary>
public sealed record This : Rewrite;

/// <summary>`owner`: whoever has the other relation on the same object.</summary>
public sealed record ComputedUserset(string Relation) : Rewrite;

/// <summary>`viewer from parent`: follow the tuples of `Tupleset` to other objects, and ask `Relation` there.</summary>
public sealed record TupleToUserset(string Tupleset, string Relation) : Rewrite;

public sealed record Union(IReadOnlyList<Rewrite> Children) : Rewrite;

public sealed record Intersection(IReadOnlyList<Rewrite> Children) : Rewrite;

/// <summary>Type and relation to rewrite, for example ("document", "viewer").</summary>
public sealed class AuthorizationModel : Dictionary<(string Type, string Relation), Rewrite>;

/// <summary>A small in-memory tuple store with a Zanzibar-style check, the same shape as OpenFGA.</summary>
public sealed partial class RelationStore(AuthorizationModel model)
{
    private readonly List<RelationTuple> _tuples = [];

    public void Write(RelationTuple tuple)
    {
        if (!_tuples.Contains(tuple)) _tuples.Add(tuple);
    }

    public void Delete(RelationTuple tuple) => _tuples.Remove(tuple);

    private static string TypeOf(string obj) => obj[..obj.IndexOf(':')];

    /// <summary>Is `user` (for example "user:bob") related to `obj` (for example "document:d1") by `relation`?</summary>
    public bool Check(string user, string relation, string obj) => Check(user, relation, obj, []);
}
