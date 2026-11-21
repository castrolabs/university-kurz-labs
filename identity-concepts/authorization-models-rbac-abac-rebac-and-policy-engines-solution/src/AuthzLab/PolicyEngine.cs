namespace AuthzLab;

/// <summary>A tiny policy decision point: default deny, permit and forbid policies, forbid wins.</summary>
public sealed class PolicyEngine(IEnumerable<Policy> policies)
{
    public Decision Decide(Request request)
    {
        var permits = new List<string>();
        var forbids = new List<string>();
        var errors = new List<string>();

        foreach (var policy in policies)
        {
            bool satisfied;
            try
            {
                satisfied = policy.When(request);
            }
            catch (Exception e)
            {
                errors.Add($"{policy.Id}: {e.Message}");
                satisfied = policy.Effect == Effect.Forbid;
            }

            if (!satisfied) continue;
            (policy.Effect == Effect.Permit ? permits : forbids).Add(policy.Id);
        }

        if (forbids.Count > 0) return new Decision(false, forbids, errors);
        if (permits.Count > 0) return new Decision(true, permits, errors);
        return new Decision(false, [], errors);
    }
}
