namespace RagLab;

public static class HeadingChunker
{
    /// <summary>One chunk per "## " section, prefixed with "Title > Heading". Long sections are cut into word windows.</summary>
    public static IReadOnlyList<string> Chunk(string title, string markdown, int maxWords)
    {
        var chunks = new List<string>();
        string heading = "";
        var body = new List<string>();

        void Flush()
        {
            string text = string.Join(' ', body).Trim();
            body.Clear();
            if (text.Length == 0)
            {
                return;
            }

            string prefix = heading.Length == 0 ? title : $"{title} > {heading}";
            foreach (var piece in FixedChunker.Chunk(text, maxWords, 0))
            {
                chunks.Add($"{prefix}\n{piece}");
            }
        }

        foreach (var line in markdown.Split('\n'))
        {
            if (line.StartsWith("## "))
            {
                Flush();
                heading = line[3..].Trim();
            }
            else if (!line.StartsWith("# "))
            {
                body.Add(line.Trim());
            }
        }

        Flush();
        return chunks;
    }
}
