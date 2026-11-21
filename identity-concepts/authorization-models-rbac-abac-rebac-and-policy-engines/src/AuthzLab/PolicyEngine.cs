namespace AuthzLab;

/// <summary>A tiny policy decision point: default deny, permit and forbid policies, forbid wins.</summary>
public sealed class PolicyEngine(IEnumerable<Policy> policies)
{
    public Decision Decide(Request request)
    {
        // TODO-02: Evaluate every policy against the request.
        //          - Default deny: no satisfied permit means the answer is not allowed (and Determining is empty).
        //          - Any satisfied Forbid wins over every Permit, whatever their order.
        //          - Determining lists the ids of the policies that decided: the satisfied forbids on a deny by forbid,
        //            the satisfied permits on an allow.
        //
        // TODO-03: Policies can fail at run time (a missing attribute, a wrong type). Never let the exception escape and
        //          never lose it: add "<policy id>: <message>" to Errors. A Permit that fails is skipped, as if it did not
        //          match. A Forbid that fails is treated as SATISFIED: when a guardrail cannot be evaluated, fail closed.
        throw new NotImplementedException();
    }
}
