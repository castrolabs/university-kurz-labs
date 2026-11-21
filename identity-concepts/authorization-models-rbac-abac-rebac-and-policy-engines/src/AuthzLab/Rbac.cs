namespace AuthzLab;

public sealed record Permission(string Action, string ResourceType);

/// <summary>NIST RBAC (ANSI INCITS 359-2004): users get roles, roles get permissions, roles inherit, some roles exclude each other.</summary>
public sealed class RbacModel
{
    private readonly Dictionary<string, HashSet<string>> _juniors = new();
    private readonly Dictionary<string, HashSet<Permission>> _permissions = new();
    private readonly Dictionary<string, HashSet<string>> _userRoles = new();
    private readonly List<(string A, string B)> _conflicts = [];

    /// <summary>The senior role inherits every permission of the junior role.</summary>
    public void Inherit(string senior, string junior) => Set(_juniors, senior).Add(junior);

    public void Grant(string role, Permission permission) => Set(_permissions, role).Add(permission);

    private static HashSet<T> Set<T>(Dictionary<string, HashSet<T>> map, string key) =>
        map.TryGetValue(key, out var set) ? set : map[key] = [];

    /// <summary>Static separation of duty: no user may hold both roles, directly or through inheritance.</summary>
    public void Conflict(string roleA, string roleB) => _conflicts.Add((roleA, roleB));

    /// <summary>The role itself and every role it inherits, at any depth.</summary>
    public IReadOnlySet<string> Closure(string role)
    {
        // TODO-00: Walk the hierarchy (the `_juniors` map) transitively and return the role plus every junior.
        //          Roles can be declared in any order and a (wrong) cycle must not loop forever.
        throw new NotImplementedException();
    }

    public bool IsAllowed(string user, Permission permission) =>
        _userRoles.GetValueOrDefault(user, []).SelectMany(Closure).Any(role => _permissions.GetValueOrDefault(role, []).Contains(permission));

    /// <summary>Gives the user the role. Returns false, and changes nothing, when that would break a separation of duty.</summary>
    public bool Assign(string user, string role)
    {
        // TODO-01: Compute the roles the user would have after the assignment, inheritance included. If both roles of a
        //          conflict pair (`_conflicts`) are in that set, refuse: return false and do not assign.
        //          Otherwise record the assignment and return true.
        throw new NotImplementedException();
    }
}
