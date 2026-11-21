namespace VectorSearchLab;

/// <summary>The embeddinggemma conventions: queries and documents are embedded with different prefixes.</summary>
public static class TaskPrefixes
{
    public static string Query(string content)
    {
        // TODO-09 (optional): The embeddinggemma model card asks for "task: search result | query: {content}".
        throw new NotImplementedException("Not implemented yet.");
    }

    public static string Document(string? title, string content)
    {
        // TODO-09 (optional): ... and "title: {title} | text: {content}" for documents, with "none" when the
        //          title is missing or blank.
        throw new NotImplementedException("Not implemented yet.");
    }
}
