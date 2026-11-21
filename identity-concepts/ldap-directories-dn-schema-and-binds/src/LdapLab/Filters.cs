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
        throw new NotImplementedException();
    }

    // TODO-04: Parse an RFC 4515 filter string into the Filter tree. Support &, |, !, equality,
    // presence ("attr=*"), substring (unescaped '*' inside the value), >= and <=. "\2a" is a literal
    // '*', not a wildcard. Throw FilterException for anything malformed, INCLUDING text left over after
    // the first complete filter and an unclosed parenthesis. Do not support "~=".
    public static Filter Parse(string text)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
