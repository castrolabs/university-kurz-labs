using System.Text.RegularExpressions;

namespace RagLab;

public sealed record CitationCheck(IReadOnlyList<string> Cited, IReadOnlyList<string> Unknown);

public static partial class Citations
{
    [GeneratedRegex(@"\[([^\[\]]+)\]")]
    private static partial Regex Bracketed();

    public static CitationCheck Check(string answer, IReadOnlyCollection<string> sourceIds)
    {
        var cited = Bracketed().Matches(answer).Select(m => m.Groups[1].Value).Distinct().ToList();
        return new CitationCheck(cited, cited.Where(id => !sourceIds.Contains(id)).ToList());
    }

    /// <summary>A citation can point at a real source that does not hold the fact: check the cited chunks.</summary>
    public static bool CitedChunkContains(string answer, IEnumerable<ChunkRecord> sources, string fact) =>
        Check(answer, [.. sources.Select(s => s.Id)]).Cited
            .Any(id => sources.Any(s => s.Id == id && s.Text.Contains(fact)));
}
