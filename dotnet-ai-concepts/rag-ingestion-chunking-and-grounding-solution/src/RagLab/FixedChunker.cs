namespace RagLab;

/// <summary>The baseline: windows of words, blind to sentences and headings.</summary>
public static class FixedChunker
{
    public static IReadOnlyList<string> Chunk(string text, int size, int overlap)
    {
        if (size <= 0 || overlap < 0 || overlap >= size)
        {
            throw new ArgumentException("size must be positive and overlap must be smaller than size.");
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        for (int start = 0; start < words.Length; start += size - overlap)
        {
            chunks.Add(string.Join(' ', words.Skip(start).Take(size)));
            if (start + size >= words.Length)
            {
                break;
            }
        }

        return chunks;
    }
}
