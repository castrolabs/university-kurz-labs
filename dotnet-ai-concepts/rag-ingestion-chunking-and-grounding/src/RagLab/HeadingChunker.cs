namespace RagLab;

public static class HeadingChunker
{
    public static IReadOnlyList<string> Chunk(string title, string markdown, int maxWords)
    {
        // TODO-01: One chunk per "## " section. Each chunk is "{title} > {heading}", a newline, then the
        //          section text (its lines joined by a space). Ignore "# " lines and empty sections. A section
        //          longer than maxWords is cut with FixedChunker (no overlap), every piece keeping the prefix.
        //          The prefix puts the document and section name into the text that gets embedded.
        throw new NotImplementedException("Not implemented yet.");
    }
}
