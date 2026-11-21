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

    public static List<ChatMessage> Build(string question, IReadOnlyList<ChunkRecord> sources)
    {
        // TODO-04: Two messages. System: the Rules. User: FormatSources(sources), a blank line, then
        //          "Question: {question}" last. Citing [id] only works if the ids are in the prompt.
        throw new NotImplementedException("Not implemented yet.");
    }
}
