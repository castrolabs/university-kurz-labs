using System.Text.RegularExpressions;

namespace RagLab;

public static partial class SentenceChunker
{
    // ponytail: splits on ". " "! " "? " only, so "e.g. " breaks a sentence; use a real sentence segmenter if the corpus needs it.
    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex Boundary();

    public static IReadOnlyList<string> Chunk(string text, int maxWords, int overlapSentences)
    {
        var sentences = Boundary().Split(text.Trim()).Where(s => s.Length > 0).ToList();
        var chunks = new List<string>();
        var current = new List<string>();
        int fresh = 0;

        foreach (var sentence in sentences)
        {
            if (current.Count > 0 && Words(current) + Words(sentence) > maxWords)
            {
                chunks.Add(string.Join(' ', current));
                current = current.TakeLast(overlapSentences).ToList();
                if (Words(current) + Words(sentence) > maxWords)
                {
                    current.Clear();
                }

                fresh = 0;
            }

            current.Add(sentence);
            fresh++;
        }

        if (fresh > 0)
        {
            chunks.Add(string.Join(' ', current));
        }

        return chunks;
    }

    private static int Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static int Words(IEnumerable<string> sentences) => sentences.Sum(Words);
}
