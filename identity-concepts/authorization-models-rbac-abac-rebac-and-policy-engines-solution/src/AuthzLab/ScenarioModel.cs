namespace AuthzLab;

/// <summary>
/// The same scenario as a relation graph (the OpenFGA model). Clearance cannot be an expression here: it becomes data
/// (`open` and `cleared` tuples) that the application must write.
/// </summary>
public static class ScenarioModel
{
    public static AuthorizationModel Model() => new()
    {
        [("org", "admin")] = Rewrite.Direct(),
        [("team", "member")] = Rewrite.Direct(),

        [("folder", "parent")] = Rewrite.Direct(),
        [("folder", "viewer")] = Rewrite.Or(Rewrite.Direct(), Rewrite.FromParent("parent", "viewer")),

        [("document", "org")] = Rewrite.Direct(),
        [("document", "parent")] = Rewrite.Direct(),
        [("document", "owner")] = Rewrite.Direct(),
        [("document", "open")] = Rewrite.Direct(),
        [("document", "cleared")] = Rewrite.Direct(),
        [("document", "viewer")] = Rewrite.Or(
            Rewrite.Direct(), Rewrite.Computed("owner"), Rewrite.FromParent("parent", "viewer"), Rewrite.FromParent("org", "admin")),
        [("document", "editor")] = Rewrite.Or(Rewrite.Computed("owner"), Rewrite.FromParent("org", "admin")),
        [("document", "clearance_ok")] = Rewrite.Or(Rewrite.Computed("open"), Rewrite.Computed("cleared")),
        [("document", "can_read")] = Rewrite.And(Rewrite.Computed("viewer"), Rewrite.Computed("clearance_ok")),
        [("document", "can_edit")] = Rewrite.And(Rewrite.Computed("editor"), Rewrite.Computed("clearance_ok")),
    };

    /// <summary>The 18 tuples of the concept.</summary>
    public static IReadOnlyList<RelationTuple> Tuples() =>
    [
        new("team:eng", "member", "user:alice"),
        new("team:eng", "member", "user:bob"),
        new("team:sales", "member", "user:dave"),
        new("org:acme", "admin", "user:carol"),

        new("folder:f-eng", "viewer", "team:eng#member"),
        new("folder:f-sales", "viewer", "team:sales#member"),

        new("document:d1", "parent", "folder:f-eng"),
        new("document:d1", "owner", "user:alice"),
        new("document:d1", "org", "org:acme"),
        new("document:d1", "open", "user:*"),

        new("document:d2", "parent", "folder:f-eng"),
        new("document:d2", "owner", "user:alice"),
        new("document:d2", "org", "org:acme"),
        new("document:d2", "cleared", "user:alice"),

        new("document:d3", "parent", "folder:f-sales"),
        new("document:d3", "owner", "user:dave"),
        new("document:d3", "org", "org:acme"),
        new("document:d3", "open", "user:*"),
    ];

    public static RelationStore Store()
    {
        var store = new RelationStore(Model());
        foreach (var tuple in Tuples()) store.Write(tuple);
        return store;
    }
}
