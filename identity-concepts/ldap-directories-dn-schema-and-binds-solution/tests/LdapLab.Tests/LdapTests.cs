using LdapLab;

namespace LdapLab.Tests;

public class DistinguishedNameTests
{
    [Fact]
    public void EscapeValue_escapes_the_characters_that_are_special_in_a_dn()
    {
        Assert.Equal("Doe\\, Jane", DistinguishedName.EscapeValue("Doe, Jane"));
        Assert.Equal("a\\+b\\\"c\\\\d\\<e\\>f\\;g", DistinguishedName.EscapeValue("a+b\"c\\d<e>f;g"));
        Assert.Equal("\\#tag", DistinguishedName.EscapeValue("#tag"));
        Assert.Equal("a#b", DistinguishedName.EscapeValue("a#b"));   // '#' only matters at the start
        Assert.Equal("\\ pad\\ ", DistinguishedName.EscapeValue(" pad "));
        Assert.Equal("nul\\00", DistinguishedName.EscapeValue("nul\0"));
    }

    [Fact]
    public void Parse_reads_multi_valued_rdns_unescapes_values_and_rejects_non_dns()
    {
        var dn = DistinguishedName.Parse(TestDirectory.Jane);
        Assert.Equal(4, dn.Rdns.Count);
        Assert.Equal(2, dn.Rdns[0].Components.Count);
        Assert.Equal(new Ava("cn", "Doe, Jane"), dn.Rdns[0].Components[0]);
        Assert.Equal(new Ava("uid", "jane"), dn.Rdns[0].Components[1]);
        Assert.Equal(TestDirectory.Jane, dn.ToString());   // written back with the comma escaped

        var hex = DistinguishedName.Parse("cn=Doe\\2C Jane,dc=org");
        Assert.Equal("Doe, Jane", hex.Rdns[0].Components[0].Value);

        var spaced = DistinguishedName.Parse("UID=Alice , OU=People,dc=example, dc=org");
        Assert.Equal(new Ava("UID", "Alice"), spaced.Rdns[0].Components[0]);
        Assert.Equal(new Ava("dc", "org"), spaced.Rdns[3].Components[0]);

        foreach (var bad in new[] { "cn=a,,dc=b", "justtext", "cn=a+" })
            Assert.Throws<FormatException>(() => DistinguishedName.Parse(bad));
    }

    [Fact]
    public void Equivalence_ignores_case_spacing_and_the_order_inside_a_multi_valued_rdn()
    {
        var a = DistinguishedName.Parse("UID=Alice , OU=People,dc=example, dc=org");
        Assert.True(a.IsEquivalentTo(DistinguishedName.Parse(TestDirectory.Alice)));

        Assert.True(DistinguishedName.Parse("uid=jane+cn=Doe\\2C Jane,dc=org")
            .IsEquivalentTo(DistinguishedName.Parse("CN=doe\\, jane+UID=JANE,dc=org")));

        Assert.False(a.IsEquivalentTo(DistinguishedName.Parse("uid=alice,ou=groups,dc=example,dc=org")));
        Assert.False(a.IsEquivalentTo(DistinguishedName.Parse("ou=people,dc=example,dc=org")));
    }
}

public class FilterTests
{
    private const string Textbook = "*)(uid=*))(|(uid=*";

    [Fact]
    public void Escape_turns_the_syntax_characters_into_hex_and_builds_the_safe_lookup()
    {
        Assert.Equal("\\2a\\29\\28uid=\\2a\\29\\29\\28|\\28uid=\\2a", LdapFilter.Escape(Textbook));
        Assert.Equal("a\\5cb", LdapFilter.Escape("a\\b"));
        Assert.Equal("x\\00", LdapFilter.Escape("x\0"));
        Assert.Equal("plain value", LdapFilter.Escape("plain value"));

        Assert.Equal("(&(uid=\\2a\\29\\28uid=\\2a\\29\\29\\28|\\28uid=\\2a)(departmentNumber=sales))",
            SafeFilters.UserLookup(Textbook, "sales"));
        Assert.Equal("(&(uid=alice)(departmentNumber=finance))", SafeFilters.UserLookup("alice", "finance"));
    }

    [Fact]
    public void Parse_builds_the_tree_and_keeps_an_escaped_star_literal()
    {
        var f = LdapFilter.Parse("(&(objectClass=inetOrgPerson)(|(uid=alice)(uid=bob))(!(sn=x))(mail=*)(uid=user01*)(age>=18))");
        var and = Assert.IsType<AndFilter>(f);
        Assert.Equal(6, and.Items.Count);
        Assert.IsType<EqualityFilter>(and.Items[0]);
        Assert.Equal(2, Assert.IsType<OrFilter>(and.Items[1]).Items.Count);
        Assert.IsType<NotFilter>(and.Items[2]);
        Assert.Equal(new PresenceFilter("mail"), and.Items[3]);
        Assert.Equal(["user01", ""], Assert.IsType<SubstringFilter>(and.Items[4]).Parts);
        Assert.IsType<GreaterOrEqualFilter>(and.Items[5]);

        // an escaped star is a literal value, not a wildcard
        Assert.Equal(new EqualityFilter("uid", "a*"), LdapFilter.Parse("(uid=a\\2a)"));
        Assert.Equal(new EqualityFilter("cn", "(x)\\"), LdapFilter.Parse("(cn=\\28x\\29\\5c)"));
    }

    [Fact]
    public void Parse_rejects_malformed_filters()
    {
        string[] bad =
        [
            "(uid=alice",                       // unclosed: libldap says "Bad search filter (-7)"
            "(uid=alice))",                     // text left over
            "uid=alice",                        // no parentheses
            "(=alice)",                         // no attribute
            "(uid=\\zz)",                       // bad escape
            "(&(uid=*)(uid=*))(|(uid=*)(departmentNumber=sales))",   // the textbook payload, after concatenation
        ];
        foreach (var filter in bad) Assert.Throws<FilterException>(() => LdapFilter.Parse(filter));
    }
}

public class SearchTests
{
    private readonly InMemoryDirectory _dir = TestDirectory.Build();

    [Fact]
    public void Scopes_base_one_and_sub()
    {
        Assert.Single(_dir.Search("dc=example,dc=org", SearchScope.Base, "(objectClass=*)"));
        Assert.Equal(3, _dir.Search("dc=example,dc=org", SearchScope.OneLevel, "(objectClass=*)").Count);
        Assert.Equal(10, _dir.Search("dc=example,dc=org", SearchScope.Subtree, "(objectClass=*)").Count);
        Assert.Equal(4, _dir.Search("OU=People, dc=example,dc=org", SearchScope.Subtree, "(objectClass=*)").Count);
        Assert.Equal(3, _dir.Search("ou=people,dc=example,dc=org", SearchScope.OneLevel, "(objectClass=*)").Count);
    }

    [Fact]
    public void Filters_combine_ignore_case_and_an_undefined_attribute_matches_nothing()
    {
        const string root = "dc=example,dc=org";
        Assert.Single(_dir.Search(root, SearchScope.Subtree, "(UID=ALICE)"));
        Assert.Equal(2, _dir.Search(root, SearchScope.Subtree, "(&(objectClass=inetOrgPerson)(departmentNumber=sales))").Count);
        Assert.Equal(2, _dir.Search(root, SearchScope.Subtree, "(|(uid=alice)(uid=bob))").Count);
        Assert.Single(_dir.Search(root, SearchScope.Subtree, "(&(departmentNumber=sales)(!(uid=bob)))"));
        Assert.Single(_dir.Search(root, SearchScope.Subtree, "(mail=*)"));
        Assert.Single(_dir.Search(root, SearchScope.Subtree, "(uid=al*)"));

        // an undefined attribute matches nothing (it is not an error), a missing base is one
        Assert.Empty(_dir.Search("dc=example,dc=org", SearchScope.Subtree, "(nosuch=1)"));
        Assert.Throws<NoSuchObjectException>(() => _dir.Search("ou=nowhere,dc=example,dc=org", SearchScope.Subtree, "(objectClass=*)"));
    }

    [Fact]
    public void Concatenated_input_becomes_filter_syntax_and_escaped_input_does_not()
    {
        const string people = Bases.People;
        string Naive(string uid) => NaiveFilters.UserLookup(uid, "finance");
        string Safe(string uid) => SafeFilters.UserLookup(uid, "finance");

        // wildcard: "a*" finds alice
        Assert.Single(_dir.Search(people, SearchScope.Subtree, Naive("a*")));
        // extra clause: a boolean oracle on mail, the lesson's "alice)(mail=alice@*"
        Assert.Single(_dir.Search(people, SearchScope.Subtree, Naive("alice)(mail=alice@*")));
        Assert.Empty(_dir.Search(people, SearchScope.Subtree, Naive("alice)(mail=z*")));
        // the textbook payload is two filters: a strict parser refuses it
        Assert.Throws<FilterException>(() => _dir.Search(people, SearchScope.Subtree, Naive("*)(uid=*))(|(uid=*")));

        foreach (var payload in new[] { "a*", "alice)(mail=alice@*", "alice)(mail=z*", "*)(uid=*))(|(uid=*" })
            Assert.Empty(_dir.Search(people, SearchScope.Subtree, Safe(payload)));
        Assert.Single(_dir.Search(people, SearchScope.Subtree, Safe("alice")));
    }
}

public class BindTests
{
    [Fact]
    public void Bind_matrix_of_the_default_server()
    {
        var dir = TestDirectory.Build();

        var ok = dir.Bind(TestDirectory.Alice, "correct horse");
        Assert.Equal(new BindResult(BindStatus.Success, $"dn:{TestDirectory.Alice}"), ok);

        // case and spacing in the DN do not matter, the reported identity is the entry's own DN
        Assert.Equal($"dn:{TestDirectory.Alice}", dir.Bind("UID=Alice , ou=people, dc=example,dc=org", "correct horse").Identity);

        // wrong password and unknown user look the same: no user enumeration
        var wrong = dir.Bind(TestDirectory.Alice, "nope");
        var unknown = dir.Bind("uid=ghost,ou=people,dc=example,dc=org", "nope");
        Assert.Equal(BindStatus.InvalidCredentials, wrong.Status);
        Assert.Equal(wrong, unknown);

        // no bind at all is anonymous, and a DN with an empty password is refused
        Assert.Equal(new BindResult(BindStatus.Success, "anonymous"), dir.Bind("", ""));
        Assert.Equal(BindStatus.UnwillingToPerform, dir.Bind(TestDirectory.Alice, "").Status);
    }

    [Fact]
    public void A_permissive_server_turns_dn_plus_empty_password_into_a_successful_anonymous_bind()
    {
        var dir = TestDirectory.Build(allowUnauthenticatedBind: true);
        Assert.Equal(new BindResult(BindStatus.Success, "anonymous"), dir.Bind(TestDirectory.Alice, ""));
        // real passwords are still checked
        Assert.Equal(BindStatus.InvalidCredentials, dir.Bind(TestDirectory.Alice, "nope").Status);
    }
}

public class LoginTests
{
    [Fact]
    public void Naive_login_trusts_a_successful_bind_and_admits_anyone_on_a_permissive_server()
    {
        var naive = new NaiveLoginService(TestDirectory.Build(allowUnauthenticatedBind: true));
        Assert.True(naive.Login("alice", "").Ok);                // the bug
        Assert.True(naive.Login("alice", "correct horse").Ok);

        Assert.False(new NaiveLoginService(TestDirectory.Build()).Login("alice", "").Ok);   // the default server saves it
    }

    [Fact]
    public void Search_then_bind_login_returns_the_dn_and_the_groups()
    {
        var login = new LoginService(TestDirectory.Build());
        var alice = login.Login("alice", "correct horse");
        Assert.True(alice.Ok);
        Assert.Equal(TestDirectory.Alice, alice.Dn);
        Assert.Equal(["editors"], alice.Groups);

        Assert.Empty(login.Login("bob", "bobs secret").Groups);
        // a DN with an escaped comma still finds its group: the DN is escaped inside the filter
        Assert.Equal(["editors"], login.Login("jane", "janes pw").Groups);
    }

    [Fact]
    public void Search_then_bind_login_rejects_empty_passwords_wildcards_and_wrong_passwords()
    {
        foreach (var permissive in new[] { false, true })
        {
            var login = new LoginService(TestDirectory.Build(permissive));
            Assert.False(login.Login("alice", "").Ok);
            Assert.False(login.Login("a*", "x").Ok);
            Assert.False(login.Login("*", "correct horse").Ok);
            Assert.False(login.Login("alice)(uid=bob", "x").Ok);
            Assert.False(login.Login("alice", "wrong").Ok);
            Assert.False(login.Login("ghost", "x").Ok);
        }
    }
}
