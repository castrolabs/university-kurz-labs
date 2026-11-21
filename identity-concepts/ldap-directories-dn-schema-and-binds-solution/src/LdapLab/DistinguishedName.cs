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
        if (Components.Count != other.Components.Count) return false;
        var mine = Components.OrderBy(a => a.Type, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(a => a.Value, StringComparer.OrdinalIgnoreCase).ToList();
        var theirs = other.Components.OrderBy(a => a.Type, StringComparer.OrdinalIgnoreCase)
                                     .ThenBy(a => a.Value, StringComparer.OrdinalIgnoreCase).ToList();
        return mine.Zip(theirs).All(p =>
            string.Equals(p.First.Type, p.Second.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.First.Value, p.Second.Value, StringComparison.OrdinalIgnoreCase));
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
        var sb = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '\0') sb.Append("\\00");
            else if (c is '"' or '+' or ',' or ';' or '<' or '>' or '\\') sb.Append('\\').Append(c);
            else if (c == ' ' && (i == 0 || i == value.Length - 1)) sb.Append("\\ ");
            else if (c == '#' && i == 0) sb.Append("\\#");
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // TODO-01: Parse a DN string into RDNs and AVAs. Split on commas and '+' that are not escaped,
    // ignore spaces around types and values, decode both "\," and "\2C" forms, and throw
    // FormatException when a component has no "type=value" shape. The empty string is the root DN.
    public static DistinguishedName Parse(string dn)
    {
        var rdns = new List<Rdn>();
        if (dn.Length == 0) return new(rdns);
        var comps = new List<Ava>();
        foreach (var (segment, separator) in Split(dn))
        {
            comps.Add(ParseAva(segment));
            if (separator == '+') continue;
            rdns.Add(new Rdn(comps));
            comps = [];
        }
        if (comps.Count > 0) throw new FormatException("DN ends with '+'");
        return new(rdns);
    }

    private static List<(string Segment, char Separator)> Split(string dn)
    {
        var result = new List<(string, char)>();
        var current = new StringBuilder();
        for (var i = 0; i < dn.Length; i++)
        {
            var c = dn[i];
            if (c == '\\' && i + 1 < dn.Length) { current.Append(c).Append(dn[++i]); continue; }
            if (c is ',' or '+') { result.Add((current.ToString(), c)); current.Clear(); continue; }
            current.Append(c);
        }
        result.Add((current.ToString(), ','));
        return result;
    }

    private static Ava ParseAva(string segment)
    {
        var eq = segment.IndexOf('=');
        if (eq < 1) throw new FormatException($"Not a type=value component: '{segment}'");
        var type = segment[..eq].Trim();
        if (type.Length == 0) throw new FormatException($"Empty attribute type in '{segment}'");
        var raw = segment[(eq + 1)..].TrimStart();
        while (raw.EndsWith(' '))
        {
            var backslashes = 0;
            for (var j = raw.Length - 2; j >= 0 && raw[j] == '\\'; j--) backslashes++;
            if (backslashes % 2 == 1) break;
            raw = raw[..^1];
        }
        return new Ava(type, Unescape(raw));
    }

    // Basic Multilingual Plane only: a literal surrogate pair is not handled.
    private static string Unescape(string raw)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] != '\\') { bytes.AddRange(Encoding.UTF8.GetBytes(raw[i].ToString())); continue; }
            if (i + 1 >= raw.Length) throw new FormatException("Dangling backslash");
            if (i + 2 < raw.Length && Uri.IsHexDigit(raw[i + 1]) && Uri.IsHexDigit(raw[i + 2]))
            {
                bytes.Add(Convert.ToByte(raw.Substring(i + 1, 2), 16));
                i += 2;
            }
            else
            {
                bytes.AddRange(Encoding.UTF8.GetBytes(raw[i + 1].ToString()));
                i++;
            }
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
