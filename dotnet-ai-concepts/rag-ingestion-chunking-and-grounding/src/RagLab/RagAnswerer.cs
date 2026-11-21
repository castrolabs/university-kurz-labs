#pragma warning disable CS9113 // parameters are used once TODO-05 is done
using Microsoft.Extensions.AI;

namespace RagLab;

public sealed class RagAnswerer(RagStore store, IChatClient model, int k = 3, double minScore = 0.2)
{
    public async Task<Answer> AskAsync(string question)
    {
        // TODO-05: Retrieve k chunks and keep only those scoring at least minScore. If none is left, answer
        //          PromptBuilder.Refusal with no sources WITHOUT calling the model. Otherwise send
        //          PromptBuilder.Build(...) to the model with ChatOptions.Temperature = 0 and return its text
        //          with the ids of the chunks that were sent.
        throw new NotImplementedException("Not implemented yet.");
    }
}
