namespace RagLab;

public static class Retriever
{
    public static async Task<IReadOnlyList<Retrieved>> SearchAsync(RagStore store, string question, int k)
    {
        // TODO-03: Search the chunks collection with the question TEXT (the store embeds it) and return the
        //          top k as Retrieved(record, score), best first. A missing score counts as 0.
        throw new NotImplementedException("Not implemented yet.");
    }
}
