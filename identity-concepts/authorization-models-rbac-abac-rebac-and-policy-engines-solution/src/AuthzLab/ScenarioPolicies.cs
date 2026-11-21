namespace AuthzLab;

/// <summary>The scenario rules written as ABAC policies (the shape OPA and Cedar give them).</summary>
public static class ScenarioPolicies
{
    public static IReadOnlyList<Policy> Build() =>
    [
        new("owner", Effect.Permit, r =>
            r.Action is "read" or "edit" && r.Resource.Get<string>("owner") == r.Subject.Id),
        new("team-read", Effect.Permit, r =>
            r.Action == "read" && r.Subject.Get<string[]>("teams").Contains(r.Resource.Get<string>("team"))),
        new("admin", Effect.Permit, r => r.Subject.Get<bool>("admin")),
        new("clearance-guardrail", Effect.Forbid, r =>
            r.Resource.Get<bool>("confidential") && r.Subject.Get<int>("clearance") < r.Resource.Get<int>("level")),
    ];

    /// <summary>A contractor may only act between 09:00 (inclusive) and 17:00 (exclusive); the hour comes from the request context.</summary>
    public static Policy OfficeHoursGuard() =>
        new("contractor-office-hours", Effect.Forbid, r =>
        {
            if (!r.Subject.Get<bool>("contractor")) return false;
            var hour = (int)r.Context!["hour"]!;   // a missing or malformed hour throws: the engine then fails closed
            return hour < 9 || hour >= 17;
        });
}
