namespace VectorSearchLab;

/// <summary>The embeddinggemma conventions: queries and documents are embedded with different prefixes.</summary>
public static class TaskPrefixes
{
    public static string Query(string content) => $"task: search result | query: {content}";

    public static string Document(string? title, string content) =>
        $"title: {(string.IsNullOrWhiteSpace(title) ? "none" : title)} | text: {content}";
}
