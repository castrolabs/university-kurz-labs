namespace LdapLab;

public sealed record LoginResult(bool Ok, string? Dn, IReadOnlyList<string> Groups, string? Reason)
{
    public static LoginResult Reject(string reason) => new(false, null, [], reason);
}

public static class Bases
{
    public const string People = "ou=people,dc=example,dc=org";
    public const string Groups = "ou=groups,dc=example,dc=org";
}

/// <summary>
/// The bug from the lesson: "the bind succeeded, so the password is right". Against a server that
/// lets a DN + empty password through as anonymous, this logs in anyone.
/// </summary>
public sealed class NaiveLoginService(InMemoryDirectory directory)
{
    public LoginResult Login(string uid, string password)
    {
        var matches = directory.Search(Bases.People, SearchScope.Subtree, $"(uid={LdapFilter.Escape(uid)})");
        if (matches.Count != 1) return LoginResult.Reject("no unique match");
        var dn = matches[0].Dn.ToString();
        return directory.Bind(dn, password).Status == BindStatus.Success
            ? new LoginResult(true, dn, [], null)
            : LoginResult.Reject("bind failed");
    }
}

/// <summary>Search-then-bind login, the pattern Spring, ASP.NET and Keycloak use.</summary>
public sealed class LoginService(InMemoryDirectory directory)
{
    // TODO-08: Search-then-bind. Reject an empty password before anything else. Find the user with an
    // ESCAPED uid under Bases.People (objectClass inetOrgPerson), require exactly one match, bind as
    // that DN, and accept only if the bind succeeded AND the identity it reports is "dn:" + that DN
    // (anonymous must not count). On success return the DN and the "cn" of every group under
    // Bases.Groups (objectClass groupOfNames) whose "member" is that DN: escape the DN in the filter too.
    public LoginResult Login(string uid, string password)
    {
        throw new NotImplementedException();
    }
}
