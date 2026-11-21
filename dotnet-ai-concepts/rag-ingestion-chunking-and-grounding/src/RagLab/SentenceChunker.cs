using System.Text.RegularExpressions;

namespace RagLab;

public static partial class SentenceChunker
{
    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex Boundary();

    public static IReadOnlyList<string> Chunk(string text, int maxWords, int overlapSentences)
    {
        // TODO-00: Split the text into sentences (Boundary() splits after . ! ?), then pack WHOLE sentences
        //          into chunks of at most maxWords words (a lone sentence longer than that stays alone).
        //          When a chunk closes, the next one starts with its last overlapSentences sentences, so a fact
        //          at a boundary lives in two chunks; drop that carry-over if it leaves no room for the next
        //          sentence, and never emit a final chunk made only of carried sentences.
        throw new NotImplementedException("Not implemented yet.");
    }
}
