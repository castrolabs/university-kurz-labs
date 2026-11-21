using Microsoft.Extensions.AI;

namespace RagLab;

public static class PromptBuilder
{
    public const string Refusal = "Not in the documents.";

    public static string Rules =>
        "Answer only from the sources. Cite the id of every source you use in square brackets, like [id]. " +
        $"If the sources do not contain the answer, reply exactly: {Refusal}";

    public static string FormatSources(IEnumerable<ChunkRecord> sources) =>
        "Sources:\n" + string.Join("\n\n", sources.Select(s => $"[{s.Id}]\n{s.Text}"));

    public static List<ChatMessage> Build(string question, IReadOnlyList<ChunkRecord> sources) =>
    [
        new(ChatRole.System, Rules),
        new(ChatRole.User, $"{FormatSources(sources)}\n\nQuestion: {question}"),
    ];
}
