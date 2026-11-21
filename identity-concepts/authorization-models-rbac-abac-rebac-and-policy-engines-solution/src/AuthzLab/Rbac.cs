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
        var seen = new HashSet<string>();
        var pending = new Stack<string>([role]);
        while (pending.TryPop(out var current))
        {
            if (!seen.Add(current)) continue;
            foreach (var junior in _juniors.GetValueOrDefault(current, [])) pending.Push(junior);
        }
        return seen;
    }

    public bool IsAllowed(string user, Permission permission) =>
        _userRoles.GetValueOrDefault(user, []).SelectMany(Closure).Any(role => _permissions.GetValueOrDefault(role, []).Contains(permission));

    /// <summary>Gives the user the role. Returns false, and changes nothing, when that would break a separation of duty.</summary>
    public bool Assign(string user, string role)
    {
        var roles = Set(_userRoles, user).Append(role).SelectMany(Closure).ToHashSet();
        if (_conflicts.Any(c => roles.Contains(c.A) && roles.Contains(c.B))) return false;
        Set(_userRoles, user).Add(role);
        return true;
    }
}
