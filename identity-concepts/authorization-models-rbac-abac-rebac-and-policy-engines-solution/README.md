# Authorization Models: RBAC, ABAC, ReBAC and Policy Engines

## Goal

Authorization is one function: `decision = f(subject, action, resource, context)`. Build a small in-memory evaluator
with NIST RBAC (hierarchy and separation of duty), ABAC (attributes of the subject, the resource and the context, with
default deny and "forbid wins"), and ReBAC (relation tuples and userset rewrites in the Zanzibar and OpenFGA style).
Then ask the 12 questions of the concept to both the ABAC and the ReBAC evaluator and watch them agree.

## Prerequisites

- The difference between a PEP (enforces) and a PDP (decides)
- Roles, attributes and relationships as sources of a decision
- .NET 10 SDK

## Task

The concept ran one scenario (documents, folders, teams) through OPA, Cedar and OpenFGA, which gave identical
decisions on 12 questions. This lab has none of them: no OPA, no Cedar, no OpenFGA, no Docker, no network. The same
scenario is in `Scenario.cs` (users, documents and their attributes) and `ScenarioModel.cs` (the relation model and the
18 tuples), and the same 12 questions are the tests. The rules: the owner reads and edits; the owner's team reads; an
admin reads and edits; a confidential document needs `clearance >= level` for everyone, admin included.

Read `Models.cs`, `Scenario.cs` and `ScenarioModel.cs` first (you do not edit them). Clearance cannot be an expression in
the relation graph, so `ScenarioModel` turns it into data (`open` and `cleared` tuples): notice what that costs.

## Instructions

Complete the following TODOs (already done in this solution):

- TODO-00: `RbacModel.Closure`: a role plus every role it inherits, at any depth, safe against cycles.
- TODO-01: `RbacModel.Assign`: refuse an assignment that breaks a static separation of duty, including through inheritance.
- TODO-02: `PolicyEngine.Decide`: default deny, any satisfied forbid wins, report the determining policy ids.
- TODO-03: `PolicyEngine.Decide`: a failing permit is skipped, a failing forbid fails closed, errors are reported and never lost.
- TODO-04: `ScenarioPolicies.Build`: the four ABAC policies of the scenario (`owner`, `team-read`, `admin`, `clearance-guardrail`).
- TODO-05: `RelationStore.Direct`: a tuple for the exact user, the `user:*` wildcard, or a userset such as `team:eng#member`.
- TODO-06: `RelationStore.Check` and `Evaluate`: the rewrites (this, computed, `viewer from parent`, union, intersection) and a cycle guard.
- TODO-07: `RelationStore.ListObjects`: "what can this user see?".
- TODO-08: `DocumentApi.Get`: authorize the object on every request, with the token scope as a ceiling and not a grant.

Notice in the tests why carol, an admin, cannot read the confidential `d2` (a guardrail beats a permit), what a
typo in a forbid does to the decision, and why a scope in the token never replaces the object check.

Run the tests until they all pass.

## Running the Lab

```bash
dotnet test
```

## Bonus (Optional)

The test of the optional TODO carries `Trait("Optional", "true")`. Skip it with `dotnet test --filter "Optional!=true"`.

- TODO-09 (optional): `ScenarioPolicies.OfficeHoursGuard`: contractors may only act between 09:00 and 17:00, with the hour coming from the request context (an environment attribute).

## What is docs only

OPA, Cedar and OpenFGA themselves, XACML, the Zanzibar "new enemy problem" and its consistency tokens, partial
evaluation, formal analysis and the latency numbers cannot be reproduced by a 200-line evaluator. The concept
measured them: read it for the engines, use this lab for the models.

## Notes on the solution

- `Closure` is an iterative walk with a seen-set, so a mistaken cycle terminates. `Assign` computes the roles the user
  WOULD have (the new role plus its closure) before touching any state, so a refusal changes nothing.
- `Decide` evaluates every policy (no short circuit) so `Determining` and `Errors` are complete. The one deliberate
  difference from Cedar: Cedar skips a policy that errors, which in the concept let carol into a confidential document when
  the typo was in the `forbid`. Here a failing `Forbid` counts as satisfied. Default deny, validation in CI and alerts on
  non-empty `Errors` are the real defenses; this is the last line.
- `Check` keeps `(user, relation, object)` in a `visiting` set and removes it in `finally`: a cycle is cut, while two
  different paths to the same node (a diamond) are both explored.
- `Direct` covers the three shapes of a tuple's user side. The userset case (`team:eng#member`) recurses into `Check`,
  which is how bob becomes a viewer of a folder without a tuple of his own.
- `ListObjects` scans the objects of the type. It answers the right question; real engines (OpenFGA `list-objects`,
  SpiceDB lookup) avoid the full scan, and RBAC or plain attribute engines answer it badly, as the concept notes.
- `DocumentApi` orders its answers 401, scope 403, 404, object 403. Do not skip the last check because the UI never
  showed a link: that is BOLA (OWASP API1:2023).
- Role explosion, for scale: 20 teams times 2 actions times 4 clearance levels is 160 roles for one document type with
  RBAC alone. The ABAC guardrail above is one line.
