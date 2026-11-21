using Microsoft.Extensions.AI;

namespace RagLab;

public sealed class RagAnswerer(RagStore store, IChatClient model, int k = 3, double minScore = 0.2)
{
    public async Task<Answer> AskAsync(string question)
    {
        var relevant = (await Retriever.SearchAsync(store, question, k))
            .Where(r => r.Score >= minScore)
            .Select(r => r.Chunk)
            .ToList();
        if (relevant.Count == 0)
        {
            return new Answer(PromptBuilder.Refusal, []);
        }

        var response = await model.GetResponseAsync(
            PromptBuilder.Build(question, relevant), new ChatOptions { Temperature = 0 });
        return new Answer(response.Text, relevant.Select(c => c.Id).ToList());
    }
}
