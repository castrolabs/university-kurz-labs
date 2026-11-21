namespace AuthzLab;

/// <summary>The scenario rules written as ABAC policies (the shape OPA and Cedar give them).</summary>
public static class ScenarioPolicies
{
    public static IReadOnlyList<Policy> Build()
    {
        // TODO-04: Write four policies with these ids, over the attributes of Scenario (User: teams, clearance, admin;
        //          Doc: owner, team, confidential, level) and the request action ("read" or "edit"):
        //          - "owner": Permit read and edit when the document's owner is the subject.
        //          - "team-read": Permit read when the document's team is one of the subject's teams.
        //          - "admin": Permit any action to an admin.
        //          - "clearance-guardrail": Forbid when the document is confidential and the subject's clearance is
        //            below its level. It applies to everyone, admins included: a guardrail written once.
        //          Read attributes with entity.Get<T>(name): a typo then shows up as an evaluation error.
        throw new NotImplementedException();
    }

    /// <summary>A contractor may only act between 09:00 (inclusive) and 17:00 (exclusive); the hour comes from the request context.</summary>
    public static Policy OfficeHoursGuard()
    {
        // TODO-09 (optional): A Forbid with the id "contractor-office-hours": it applies when the subject is a contractor
        //                    (attribute "contractor") AND the context "hour" (an int) is outside [9, 17).
        //                    Check the contractor attribute first, so a non-contractor needs no "hour" at all.
        throw new NotImplementedException();
    }
}
