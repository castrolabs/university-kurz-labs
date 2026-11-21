using LdapLab;

namespace LdapLab.Tests;

/// <summary>The lab tree: alice and bob, "Doe, Jane" (a DN with an escaped comma), two groups and a service account.</summary>
public static class TestDirectory
{
    public const string Alice = "uid=alice,ou=people,dc=example,dc=org";
    public const string Jane = "cn=Doe\\, Jane+uid=jane,ou=people,dc=example,dc=org";

    private static string Pw(string password, byte salt) => Ssha.Hash(password, [salt, 2, 3, 4]);

    public static InMemoryDirectory Build(bool allowUnauthenticatedBind = false)
    {
        var d = new InMemoryDirectory(allowUnauthenticatedBind);
        d.Add("dc=example,dc=org", new Dictionary<string, string[]> { ["objectClass"] = ["top", "dcObject"], ["dc"] = ["example"] });
        foreach (var ou in new[] { "people", "groups", "services" })
            d.Add($"ou={ou},dc=example,dc=org", new Dictionary<string, string[]> { ["objectClass"] = ["organizationalUnit"], ["ou"] = [ou] });
        d.Add(Alice, new Dictionary<string, string[]>
        {
            ["objectClass"] = ["inetOrgPerson"], ["uid"] = ["alice"], ["cn"] = ["Alice A"], ["sn"] = ["A"],
            ["mail"] = ["alice@example.org"], ["departmentNumber"] = ["finance"], ["userPassword"] = [Pw("correct horse", 1)],
        });
        d.Add("uid=bob,ou=people,dc=example,dc=org", new Dictionary<string, string[]>
        {
            ["objectClass"] = ["inetOrgPerson"], ["uid"] = ["bob"], ["cn"] = ["Bob B"], ["sn"] = ["B"],
            ["departmentNumber"] = ["sales"], ["userPassword"] = [Pw("bobs secret", 9)],
        });
        d.Add(Jane, new Dictionary<string, string[]>
        {
            ["objectClass"] = ["inetOrgPerson"], ["uid"] = ["jane"], ["cn"] = ["Doe, Jane"], ["sn"] = ["Doe"],
            ["departmentNumber"] = ["sales"], ["userPassword"] = [Pw("janes pw", 5)],
        });
        d.Add("cn=editors,ou=groups,dc=example,dc=org", new Dictionary<string, string[]>
        {
            ["objectClass"] = ["groupOfNames"], ["cn"] = ["editors"], ["member"] = [Alice, Jane],
        });
        d.Add("cn=auditors,ou=groups,dc=example,dc=org", new Dictionary<string, string[]>
        {
            ["objectClass"] = ["groupOfNames"], ["cn"] = ["auditors"], ["member"] = ["uid=svc-search,ou=services,dc=example,dc=org"],
        });
        d.Add("uid=svc-search,ou=services,dc=example,dc=org", new Dictionary<string, string[]>
        {
            ["objectClass"] = ["account"], ["uid"] = ["svc-search"], ["userPassword"] = [Pw("svc", 7)],
        });
        return d;
    }
}
