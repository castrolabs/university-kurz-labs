using System.Text;

namespace LdapLab;

/// <summary>One attribute type and value pair, such as cn=Doe, Jane (already unescaped).</summary>
public sealed record Ava(string Type, string Value);

/// <summary>A relative distinguished name: one or more Ava joined by '+'.</summary>
public sealed class Rdn(IReadOnlyList<Ava> components)
{
    public IReadOnlyList<Ava> Components { get; } = components;

    public override string ToString() =>
        string.Join("+", Components.Select(a => $"{a.Type}={DistinguishedName.EscapeValue(a.Value)}"));

    // TODO-02: Two RDNs are equivalent when they have the same components, ignoring case in types and
    // values and ignoring the order of the components of a multi-valued RDN
    // ("UID=Alice" equals "uid=alice", "uid=jane+cn=Doe" equals "cn=doe+uid=jane").
    public bool IsEquivalentTo(Rdn other)
    {
        throw new NotImplementedException();
    }
}

/// <summary>An RFC 4514 distinguished name: the leaf RDN first, the root last.</summary>
public sealed class DistinguishedName(IReadOnlyList<Rdn> rdns)
{
    public IReadOnlyList<Rdn> Rdns { get; } = rdns;

    public DistinguishedName Parent => new(Rdns.Skip(1).ToList());

    public override string ToString() => string.Join(",", Rdns);

    public bool IsEquivalentTo(DistinguishedName other) =>
        Rdns.Count == other.Rdns.Count && Rdns.Zip(other.Rdns).All(p => p.First.IsEquivalentTo(p.Second));

    public bool IsDescendantOrSelfOf(DistinguishedName ancestor)
    {
        if (Rdns.Count < ancestor.Rdns.Count) return false;
        return new DistinguishedName(Rdns.Skip(Rdns.Count - ancestor.Rdns.Count).ToList()).IsEquivalentTo(ancestor);
    }

    // TODO-00: Escape an attribute value for use inside a DN (RFC 4514 section 2.4): the characters
    // that are special anywhere, a leading space or '#', a trailing space, and the NUL character.
    public static string EscapeValue(string value)
    {
        throw new NotImplementedException();
    }

    // TODO-01: Parse a DN string into RDNs and AVAs. Split on commas and '+' that are not escaped,
    // ignore spaces around types and values, decode both "\," and "\2C" forms, and throw
    // FormatException when a component has no "type=value" shape. The empty string is the root DN.
    public static DistinguishedName Parse(string dn)
    {
        throw new NotImplementedException();
    }

}
