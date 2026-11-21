using System.Text;

namespace LdapLab;

public sealed class FilterException(string message) : Exception(message);

public abstract record Filter;
public sealed record AndFilter(IReadOnlyList<Filter> Items) : Filter;
public sealed record OrFilter(IReadOnlyList<Filter> Items) : Filter;
public sealed record NotFilter(Filter Item) : Filter;
public sealed record EqualityFilter(string Attribute, string Value) : Filter;
public sealed record PresenceFilter(string Attribute) : Filter;
/// <summary>Parts are the literal pieces between wildcards: "user01*" is ["user01", ""], "*ab*" is ["", "ab", ""].</summary>
public sealed record SubstringFilter(string Attribute, IReadOnlyList<string> Parts) : Filter;
public sealed record GreaterOrEqualFilter(string Attribute, string Value) : Filter;
public sealed record LessOrEqualFilter(string Attribute, string Value) : Filter;

public static class FilterEvaluator
{
    /// <summary>Matches case-insensitively, like caseIgnoreMatch. An undefined attribute never matches (it is not an error).</summary>
    public static bool Matches(this Entry entry, Filter filter) => filter switch
    {
        AndFilter f => f.Items.All(entry.Matches),
        OrFilter f => f.Items.Any(entry.Matches),
        NotFilter f => !entry.Matches(f.Item),
        PresenceFilter f => entry.Values(f.Attribute).Length > 0,
        EqualityFilter f => entry.Values(f.Attribute).Any(v => string.Equals(v, f.Value, StringComparison.OrdinalIgnoreCase)),
        GreaterOrEqualFilter f => entry.Values(f.Attribute).Any(v => string.Compare(v, f.Value, StringComparison.OrdinalIgnoreCase) >= 0),
        LessOrEqualFilter f => entry.Values(f.Attribute).Any(v => string.Compare(v, f.Value, StringComparison.OrdinalIgnoreCase) <= 0),
        SubstringFilter f => entry.Values(f.Attribute).Any(v => MatchesSubstring(v, f.Parts)),
        _ => throw new NotSupportedException(filter.GetType().Name),
    };

    private static bool MatchesSubstring(string value, IReadOnlyList<string> parts)
    {
        var first = parts[0];
        var last = parts[^1];
        if (!value.StartsWith(first, StringComparison.OrdinalIgnoreCase)) return false;
        var pos = first.Length;
        for (var i = 1; i < parts.Count - 1; i++)
        {
            var idx = value.IndexOf(parts[i], pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            pos = idx + parts[i].Length;
        }
        return value.Length - pos >= last.Length && value.EndsWith(last, StringComparison.OrdinalIgnoreCase);
    }
}

public static class LdapFilter
{
    // TODO-03: Escape a value for use inside a filter (RFC 4515 section 3): the four characters
    // that are syntax in a filter, and NUL, each as a backslash and two hexadecimal digits.
    public static string Escape(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            switch (c)
            {
                case '*': sb.Append("\\2a"); break;
                case '(': sb.Append("\\28"); break;
                case ')': sb.Append("\\29"); break;
                case '\\': sb.Append("\\5c"); break;
                case '\0': sb.Append("\\00"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    // TODO-04: Parse an RFC 4515 filter string into the Filter tree. Support &, |, !, equality,
    // presence ("attr=*"), substring (unescaped '*' inside the value), >= and <=. "\2a" is a literal
    // '*', not a wildcard. Throw FilterException for anything malformed, INCLUDING text left over after
    // the first complete filter and an unclosed parenthesis. Do not support "~=".
    public static Filter Parse(string text)
    {
        var pos = 0;
        var filter = ParseFilter(text, ref pos);
        if (pos != text.Length) throw new FilterException($"Unexpected text after the filter at position {pos}");
        return filter;
    }

    private static Filter ParseFilter(string s, ref int pos)
    {
        Expect(s, ref pos, '(');
        if (pos >= s.Length) throw new FilterException("Unexpected end of filter");
        Filter result;
        switch (s[pos])
        {
            case '&':
            case '|':
                var isAnd = s[pos++] == '&';
                var items = new List<Filter>();
                while (pos < s.Length && s[pos] == '(') items.Add(ParseFilter(s, ref pos));
                result = isAnd ? new AndFilter(items) : new OrFilter(items);
                break;
            case '!':
                pos++;
                result = new NotFilter(ParseFilter(s, ref pos));
                break;
            default:
                result = ParseItem(s, ref pos);
                break;
        }
        Expect(s, ref pos, ')');
        return result;
    }

    private static void Expect(string s, ref int pos, char c)
    {
        if (pos >= s.Length || s[pos] != c) throw new FilterException($"Expected '{c}' at position {pos}");
        pos++;
    }

    private static Filter ParseItem(string s, ref int pos)
    {
        var end = pos;
        while (end < s.Length && s[end] != ')' && s[end] != '(') end++;
        if (end >= s.Length || s[end] == '(') throw new FilterException("Unterminated or malformed item");
        var item = s[pos..end];
        pos = end;
        var eq = item.IndexOf('=');
        if (eq < 1) throw new FilterException($"No attribute or operator in '{item}'");
        var op = item[eq - 1] switch { '>' => ">=", '<' => "<=", '~' => "~=", _ => "=" };
        var attr = op == "=" ? item[..eq] : item[..(eq - 1)];
        if (attr.Length == 0 || !attr.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '.' or ';'))
            throw new FilterException($"Bad attribute description '{attr}'");
        var raw = item[(eq + 1)..];
        return op switch
        {
            ">=" => new GreaterOrEqualFilter(attr, Unescape(raw)),
            "<=" => new LessOrEqualFilter(attr, Unescape(raw)),
            "~=" => throw new FilterException("Approximate match is not supported"),
            _ when raw == "*" => new PresenceFilter(attr),
            _ when raw.Contains('*') => new SubstringFilter(attr, raw.Split('*').Select(Unescape).ToList()),
            _ => new EqualityFilter(attr, Unescape(raw)),
        };
    }

    // Basic Multilingual Plane only: a literal surrogate pair is not handled.
    private static string Unescape(string raw)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] != '\\') { bytes.AddRange(Encoding.UTF8.GetBytes(raw[i].ToString())); continue; }
            if (i + 2 >= raw.Length || !Uri.IsHexDigit(raw[i + 1]) || !Uri.IsHexDigit(raw[i + 2]))
                throw new FilterException("A backslash must be followed by two hexadecimal digits");
            bytes.Add(Convert.ToByte(raw.Substring(i + 1, 2), 16));
            i += 2;
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}

public static class NaiveFilters
{
    /// <summary>The bug: user input is concatenated into the filter, so it can become syntax.</summary>
    public static string UserLookup(string uid, string department) => $"(&(uid={uid})(departmentNumber={department}))";
}

public static class SafeFilters
{
    // TODO-05: Build the same "uid and department" lookup, but safe: both values must be escaped,
    // so that no input can add a wildcard, close a parenthesis or open a second filter.
    public static string UserLookup(string uid, string department)
    {
        return $"(&(uid={LdapFilter.Escape(uid)})(departmentNumber={LdapFilter.Escape(department)}))";
    }
}
