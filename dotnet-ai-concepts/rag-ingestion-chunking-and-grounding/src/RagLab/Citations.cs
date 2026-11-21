using System.Text.RegularExpressions;

namespace RagLab;

public sealed record CitationCheck(IReadOnlyList<string> Cited, IReadOnlyList<string> Unknown);

public static partial class Citations
{
    [GeneratedRegex(@"\[([^\[\]]+)\]")]
    private static partial Regex Bracketed();

    public static CitationCheck Check(string answer, IReadOnlyCollection<string> sourceIds)
    {
        // TODO-06: Cited = the distinct ids found in [square brackets], in order of appearance.
        //          Unknown = the cited ids that are not among sourceIds (a citation the model made up).
        throw new NotImplementedException("Not implemented yet.");
    }

    public static bool CitedChunkContains(string answer, IEnumerable<ChunkRecord> sources, string fact)
    {
        // TODO-07: True when at least one chunk the answer cites holds the fact text. An answer can cite a
        //          real source that does not contain the fact: reading the answer does not catch that, this does.
        throw new NotImplementedException("Not implemented yet.");
    }
}
