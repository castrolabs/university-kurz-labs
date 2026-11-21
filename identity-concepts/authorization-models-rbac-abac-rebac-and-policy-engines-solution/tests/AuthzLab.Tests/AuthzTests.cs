namespace AuthzLab.Tests;

public sealed class AuthzTests
{
    // The 12 questions of the concept. OPA, Cedar and OpenFGA gave the same decision for each of them.
    private static readonly (string User, string Action, string Doc, bool Allowed)[] TwelveQuestions =
    [
        ("alice", "read", "d1", true),
        ("alice", "edit", "d1", true),
        ("bob", "read", "d1", true),
        ("bob", "edit", "d1", false),
        ("carol", "edit", "d1", true),
        ("bob", "read", "d2", false),
        ("carol", "read", "d2", false),   // admin, but the confidential document needs clearance
        ("carol", "read", "d3", true),
        ("dave", "edit", "d3", true),
        ("dave", "read", "d1", false),
        ("alice", "read", "d2", true),
        ("alice", "read", "d3", false),
    ];

    private static PolicyEngine ScenarioEngine() => new(ScenarioPolicies.Build());

    private static string Relation(string action) => action == "read" ? "can_read" : "can_edit";

    private static readonly Permission ReadDoc = new("read", "doc");
    private static readonly Permission EditDoc = new("edit", "doc");
    private static readonly Permission DeleteDoc = new("delete", "doc");

    // --- RBAC (NIST) ---

    [Fact]
    public void ShouldInheritPermissionsDownTheRoleHierarchy()
    {
        var rbac = new RbacModel();
        rbac.Grant("viewer", ReadDoc);
        rbac.Grant("editor", EditDoc);
        rbac.Grant("admin", DeleteDoc);
        rbac.Inherit("admin", "editor");     // declared before editor inherits viewer, on purpose
        rbac.Inherit("editor", "viewer");
        rbac.Assign("ana", "admin");
        rbac.Assign("val", "viewer");

        Assert.All([ReadDoc, EditDoc, DeleteDoc], p => Assert.True(rbac.IsAllowed("ana", p)));
        Assert.True(rbac.IsAllowed("val", ReadDoc));
        Assert.False(rbac.IsAllowed("val", EditDoc));
        Assert.False(rbac.IsAllowed("nobody", ReadDoc));

        rbac.Inherit("viewer", "admin");     // a cycle by mistake must not hang the closure
        Assert.Equal(["admin", "editor", "viewer"], rbac.Closure("viewer").Order());
    }

    [Fact]
    public void ShouldRefuseAssignmentsThatBreakSeparationOfDuty()
    {
        var rbac = new RbacModel();
        rbac.Inherit("approver-lead", "approver");
        rbac.Conflict("requester", "approver");

        Assert.True(rbac.Assign("ana", "requester"));
        Assert.False(rbac.Assign("ana", "approver"));        // direct conflict
        Assert.False(rbac.Assign("ana", "approver-lead"));   // the conflict arrives through inheritance
        Assert.True(rbac.Assign("bob", "approver-lead"));
        Assert.False(rbac.Assign("bob", "requester"));
    }

    // --- the policy engine: ABAC ---

    [Fact]
    public void ShouldDenyByDefaultAndSayWhichPolicyAllowed()
    {
        Assert.False(new PolicyEngine([]).Decide(Scenario.Ask("alice", "read", "d1")).Allowed);

        var denied = ScenarioEngine().Decide(Scenario.Ask("dave", "read", "d1"));
        Assert.False(denied.Allowed);
        Assert.Empty(denied.Determining);

        var allowed = ScenarioEngine().Decide(Scenario.Ask("bob", "read", "d1"));
        Assert.True(allowed.Allowed);
        Assert.Equal(["team-read"], allowed.Determining);
    }

    [Fact]
    public void ShouldLetAForbidBeatAnAdminPermitWhateverTheOrder()
    {
        var request = Scenario.Ask("carol", "read", "d2");

        var decision = ScenarioEngine().Decide(request);
        Assert.False(decision.Allowed);
        Assert.Equal(["clearance-guardrail"], decision.Determining);

        var reversed = new PolicyEngine(ScenarioPolicies.Build().Reverse());
        Assert.False(reversed.Decide(request).Allowed);
    }

    [Fact]
    public void ShouldFailClosedWhenAForbidCannotBeEvaluated()
    {
        // The typo of the concept: "clearence". Cedar skipped the policy and let carol in; this engine must not.
        var typo = new Policy("typo-guardrail", Effect.Forbid, r => r.Subject.Get<int>("clearence") < 1);
        var engine = new PolicyEngine([.. ScenarioPolicies.Build(), typo]);

        var decision = engine.Decide(Scenario.Ask("carol", "read", "d3"));

        Assert.False(decision.Allowed);
        var error = Assert.Single(decision.Errors);
        Assert.StartsWith("typo-guardrail: ", error);
        Assert.Contains("clearence", error);
    }

    [Fact]
    public void ShouldSkipAPermitThatFailsButKeepEvaluatingTheOthers()
    {
        var broken = new Policy("broken-permit", Effect.Permit, r => r.Resource.Get<bool>("archived"));
        var engine = new PolicyEngine([broken, .. ScenarioPolicies.Build()]);

        var decision = engine.Decide(Scenario.Ask("alice", "read", "d1"));

        Assert.True(decision.Allowed);
        Assert.Contains("owner", decision.Determining);
        Assert.DoesNotContain("broken-permit", decision.Determining);
        Assert.StartsWith("broken-permit: ", Assert.Single(decision.Errors));

        Assert.False(new PolicyEngine([broken]).Decide(Scenario.Ask("alice", "read", "d1")).Allowed);
    }

    [Fact]
    public void ShouldAnswerTheTwelveQuestionsWithAttributes()
    {
        var engine = ScenarioEngine();

        foreach (var q in TwelveQuestions)
            Assert.True(q.Allowed == engine.Decide(Scenario.Ask(q.User, q.Action, q.Doc)).Allowed, $"ABAC: {q.User} {q.Action} {q.Doc}");
    }

    // --- ReBAC: relation tuples and rewrites ---

    [Fact]
    public void ShouldAnswerTheTwelveQuestionsWithRelations()
    {
        var store = ScenarioModel.Store();

        foreach (var q in TwelveQuestions)
            Assert.True(q.Allowed == store.Check($"user:{q.User}", Relation(q.Action), $"document:{q.Doc}"), $"ReBAC: {q.User} {q.Action} {q.Doc}");
    }

    [Fact]
    public void ShouldGiveTheSameDecisionInBothModelsForEveryUserDocumentAndAction()
    {
        var engine = ScenarioEngine();
        var store = ScenarioModel.Store();

        foreach (var user in new[] { "alice", "bob", "carol", "dave" })
            foreach (var doc in new[] { "d1", "d2", "d3" })
                foreach (var action in new[] { "read", "edit" })
                    Assert.True(
                        engine.Decide(Scenario.Ask(user, action, doc)).Allowed == store.Check($"user:{user}", Relation(action), $"document:{doc}"),
                        $"{user} {action} {doc}");
    }

    [Fact]
    public void ShouldInheritAccessThroughTheGraphAndLoseItWhenTheEdgeIsDeleted()
    {
        var store = ScenarioModel.Store();
        store.Write(new("document:d4", "owner", "user:alice"));
        store.Write(new("document:d4", "open", "user:*"));
        var parent = new RelationTuple("document:d4", "parent", "folder:f-eng");

        Assert.False(store.Check("user:bob", "can_read", "document:d4"));   // no team tuple, no parent yet
        store.Write(parent);
        Assert.True(store.Check("user:bob", "can_read", "document:d4"));    // viewer of the folder, through team eng
        store.Delete(parent);
        Assert.False(store.Check("user:bob", "can_read", "document:d4"));
    }

    [Fact]
    public void ShouldNotLoopForeverOnCyclesInTheGraph()
    {
        var store = new RelationStore(ScenarioModel.Model());
        store.Write(new("folder:fa", "parent", "folder:fb"));
        store.Write(new("folder:fb", "parent", "folder:fa"));
        store.Write(new("folder:fb", "viewer", "user:zed"));

        Assert.True(store.Check("user:zed", "viewer", "folder:fa"));       // found before the cycle closes
        Assert.False(store.Check("user:nobody", "viewer", "folder:fa"));   // the cycle is walked once, then cut
    }

    [Fact]
    public void ShouldListWhatEachUserCanSee()
    {
        var store = ScenarioModel.Store();

        Assert.Equal(["document:d1"], store.ListObjects("user:bob", "can_read", "document"));
        Assert.Equal(["document:d1", "document:d2"], store.ListObjects("user:alice", "can_read", "document"));
        Assert.Equal(["document:d1", "document:d3"], store.ListObjects("user:carol", "can_edit", "document"));
        Assert.Empty(store.ListObjects("user:mallory", "can_read", "document"));
    }

    // --- the trap: hiding the button, and IDOR/BOLA ---

    [Fact]
    public void ShouldAuthorizeTheObjectNotOnlyTheFunction()
    {
        var api = new DocumentApi(ScenarioEngine());
        IReadOnlySet<string> scopes = new HashSet<string> { "documents:read" };

        Assert.Equal(new ApiResult(200, "Q3 plan"), api.Get("alice", "d1", scopes));
        Assert.Equal(403, api.Get("alice", "d3", scopes).Status);   // dave's document: changing the id in the URL gets nothing
        Assert.Equal(403, api.Get("bob", "d2", scopes).Status);     // team member, but no clearance
        Assert.Equal(404, api.Get("alice", "d9", scopes).Status);
        Assert.Equal(401, api.Get("mallory", "d1", scopes).Status);
        Assert.Equal(401, api.Get(null, "d1", scopes).Status);
    }

    [Fact]
    public void ShouldTreatAScopeAsACeilingAndNeverAsAGrant()
    {
        var api = new DocumentApi(ScenarioEngine());

        // The user owns d1, but this client was never allowed to read documents on her behalf.
        Assert.Equal(403, api.Get("alice", "d1", new HashSet<string> { "profile" }).Status);
        // The client has the scope, the user has no right on dave's document: the scope does not create one.
        Assert.Equal(403, api.Get("alice", "d3", new HashSet<string> { "documents:read" }).Status);
    }

    // --- optional ---

    [Fact, Trait("Optional", "true")]
    public void ShouldForbidContractorsOutsideOfficeHours()
    {
        var engine = new PolicyEngine([.. ScenarioPolicies.Build(), ScenarioPolicies.OfficeHoursGuard()]);

        Assert.True(engine.Decide(Scenario.Ask("erin", "read", "d1", Hour(10))).Allowed);
        Assert.True(engine.Decide(Scenario.Ask("erin", "read", "d1", Hour(16))).Allowed);

        var late = engine.Decide(Scenario.Ask("erin", "read", "d1", Hour(17)));
        Assert.False(late.Allowed);
        Assert.Equal(["contractor-office-hours"], late.Determining);
        Assert.False(engine.Decide(Scenario.Ask("erin", "read", "d1", Hour(8))).Allowed);

        // An employee does not even need an hour in the request.
        Assert.True(engine.Decide(Scenario.Ask("bob", "read", "d1")).Allowed);
        // A contractor without an hour: the guard cannot be evaluated, so it fails closed.
        Assert.False(engine.Decide(Scenario.Ask("erin", "read", "d1")).Allowed);
    }

    private static Dictionary<string, object?> Hour(int hour) => new() { ["hour"] = hour };
}
