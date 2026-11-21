namespace AuthzLab;

public sealed partial class RelationStore
{
    private bool Check(string user, string relation, string obj, HashSet<string> visiting)
    {
        if (!model.TryGetValue((TypeOf(obj), relation), out var rewrite)) return false;

        // TODO-06 (part 1): Relations can form cycles (folder A has parent B, B has parent A). Remember the
        //          (user, relation, object) triples being evaluated in `visiting`; meeting one again means "no" for
        //          that path. Forget the triple when its evaluation ends, so sibling paths are not affected.
        return Evaluate(rewrite, user, relation, obj, visiting);
    }

    private bool Evaluate(Rewrite rewrite, string user, string relation, string obj, HashSet<string> visiting)
    {
        // TODO-06 (part 2): Evaluate each kind of rewrite:
        //          - This: Direct(...).
        //          - ComputedUserset: Check the other relation on the same object.
        //          - TupleToUserset: for every tuple (obj, Tupleset, other) whose user side is an object (not a userset),
        //            Check `Relation` on `other`. "viewer from parent" is this.
        //          - Union: any child. Intersection: all children ("viewer and clearance_ok").
        throw new NotImplementedException();
    }

    private bool Direct(string user, string relation, string obj, HashSet<string> visiting)
    {
        // TODO-05: Look at the tuples written for (obj, relation). The user is related when a tuple has:
        //          - exactly this user ("user:bob"),
        //          - the wildcard "user:*" (anyone of type user, like a public document),
        //          - a userset "team:eng#member": the user is related if it has `member` on "team:eng" (recurse with Check).
        throw new NotImplementedException();
    }

    /// <summary>"What can this user see?" The question a document list page needs.</summary>
    public IReadOnlyList<string> ListObjects(string user, string relation, string type)
    {
        // TODO-07: Return, ordered ordinally, every object of `type` that appears in the store and for which Check is true.
        //          (A brute-force scan is enough here; a real engine walks the graph backwards.)
        throw new NotImplementedException();
    }
}
