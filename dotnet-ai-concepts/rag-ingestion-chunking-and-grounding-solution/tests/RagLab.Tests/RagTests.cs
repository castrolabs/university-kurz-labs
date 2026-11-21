using Microsoft.Extensions.AI;

namespace RagLab.Tests;

public class RagTests
{
    private static readonly Document FileLimits = new("file-limits", "File limits", """
        # File limits
        ## Upload size
        The upload ceiling for one file for Business customers is 250 GB.
        ## Storage
        Team customers get 2 TB of storage per seat.
        """);

    private static readonly Document SyncClient = new("sync-client", "Sync client", """
        # Sync client
        ## Network
        The sync client listens on UDP port 47613 for local discovery.
        ## Logs
        Sign-in logs are kept for 30 days.
        """);

    private static readonly Document Plans = new("plans", "Plans and pricing", """
        # Plans and pricing
        ## Team plan
        The Team plan costs 9 dollars per user each month.
        """);

    private static readonly Question[] Questions =
    [
        new("What is the upload ceiling for one file", "250 GB"),
        new("How much storage does each Team seat get", "2 TB"),
        new("Which UDP port does local discovery use", "UDP port 47613"),
        new("How many days are sign-in logs kept", "30 days"),
    ];

    private static Func<Document, IReadOnlyList<string>> ByHeading => d => HeadingChunker.Chunk(d.Title, d.Markdown, 40);

    private static async Task<RagStore> NewStore(params Document[] documents)
    {
        var store = await RagStore.CreateAsync(new HashingEmbeddingGenerator());
        foreach (var document in documents)
        {
            await Ingestor.IngestAsync(store, document, ByHeading);
        }

        return store;
    }

    [Fact]
    public void Sentence_chunker_packs_whole_sentences_and_never_cuts_inside_one()
    {
        var chunks = SentenceChunker.Chunk("One two three. Four five. Six seven eight nine. Ten.", maxWords: 6, overlapSentences: 0);

        Assert.Equal(["One two three. Four five.", "Six seven eight nine. Ten."], chunks);
    }

    [Fact]
    public void Sentence_overlap_repeats_the_last_sentence_so_a_fact_sits_in_two_chunks()
    {
        var chunks = SentenceChunker.Chunk("Alpha one. Beta two. Gamma three. Delta four.", maxWords: 4, overlapSentences: 1);

        Assert.Equal(["Alpha one. Beta two.", "Beta two. Gamma three.", "Gamma three. Delta four."], chunks);
    }

    [Fact]
    public void Heading_chunker_prefixes_each_section_with_title_and_heading()
    {
        var chunks = HeadingChunker.Chunk(SyncClient.Title, SyncClient.Markdown, maxWords: 40);

        Assert.Equal(
            ["Sync client > Network\nThe sync client listens on UDP port 47613 for local discovery.",
             "Sync client > Logs\nSign-in logs are kept for 30 days."],
            chunks);
    }

    [Fact]
    public async Task Ingesting_the_same_document_twice_leaves_the_same_chunks()
    {
        var store = await NewStore(FileLimits, SyncClient);

        await Ingestor.IngestAsync(store, SyncClient, ByHeading);

        Assert.Equal(4, await store.CountAsync());
        Assert.NotNull(await store.Chunks.GetAsync("sync-client#1"));
    }

    [Fact]
    public async Task Ingesting_a_shorter_version_deletes_the_chunks_the_document_no_longer_has()
    {
        var store = await NewStore(FileLimits, SyncClient);
        var shorter = SyncClient with { Markdown = "## Network\nThe sync client listens on UDP port 47613 for local discovery." };

        await Ingestor.IngestAsync(store, shorter, ByHeading);

        Assert.Equal(3, await store.CountAsync());
        Assert.Null(await store.Chunks.GetAsync("sync-client#1"));
        var hits = await Retriever.SearchAsync(store, "how many days are sign-in logs kept", k: 3);
        Assert.DoesNotContain(hits, h => h.Chunk.Text.Contains("30 days"));
    }

    [Fact]
    public async Task Retriever_returns_the_k_best_chunks_best_first()
    {
        var store = await NewStore(FileLimits, SyncClient, Plans);

        var hits = await Retriever.SearchAsync(store, "which UDP port does local discovery use", k: 2);

        Assert.Equal(2, hits.Count);
        Assert.Equal("sync-client#0", hits[0].Chunk.Id);
        Assert.True(hits[0].Score >= hits[1].Score);
    }

    [Fact]
    public void Prompt_has_the_rules_in_a_system_message_and_the_cited_sources_then_the_question_in_a_user_message()
    {
        var sources = new[] { new ChunkRecord { Id = "file-limits#0", Text = "Ceiling is 250 GB." } };

        var messages = PromptBuilder.Build("What is the ceiling?", sources);

        Assert.Equal([ChatRole.System, ChatRole.User], messages.Select(m => m.Role));
        Assert.Contains(PromptBuilder.Refusal, messages[0].Text);
        Assert.Contains("[file-limits#0]", messages[1].Text);
        Assert.Contains("Ceiling is 250 GB.", messages[1].Text);
        Assert.EndsWith("What is the ceiling?", messages[1].Text);
    }

    [Fact]
    public async Task Answerer_sends_the_sources_to_the_model_at_temperature_zero_and_returns_their_ids()
    {
        var store = await NewStore(FileLimits, SyncClient);
        var model = new ScriptedChatClient(_ => "The ceiling is 250 GB [file-limits#0].");
        var answerer = new RagAnswerer(store, model, k: 2);

        var answer = await answerer.AskAsync("What is the upload ceiling for one file");

        Assert.Equal("The ceiling is 250 GB [file-limits#0].", answer.Text);
        Assert.Equal(1, model.Calls);
        Assert.Equal(0f, model.Options[0]!.Temperature);
        Assert.Contains("file-limits#0", answer.Sources);
        Assert.Contains("[file-limits#0]", model.Requests[0][1].Text);
    }

    [Fact]
    public async Task Answerer_refuses_without_calling_the_model_when_no_chunk_is_relevant()
    {
        var store = await NewStore(FileLimits, SyncClient);
        var model = new ScriptedChatClient(_ => "should not be called");
        var answerer = new RagAnswerer(store, model);

        var answer = await answerer.AskAsync("Do you offer a student discount");

        Assert.Equal(PromptBuilder.Refusal, answer.Text);
        Assert.Empty(answer.Sources);
        Assert.Equal(0, model.Calls);
    }

    [Fact]
    public void Citations_are_read_from_square_brackets_and_unknown_ids_are_reported()
    {
        var check = Citations.Check(
            "250 GB [file-limits#0] and 90 days [sync-client#2] [file-limits#0].", ["file-limits#0", "sync-client#0"]);

        Assert.Equal(["file-limits#0", "sync-client#2"], check.Cited);
        Assert.Equal(["sync-client#2"], check.Unknown);
        Assert.Empty(Citations.Check("Not in the documents.", ["file-limits#0"]).Cited);
    }

    [Fact]
    public void A_citation_to_a_real_source_can_still_miss_the_fact()
    {
        ChunkRecord[] sources =
        [
            new() { Id = "file-limits#0", Text = "Business: 250 GB per file." },
            new() { Id = "file-limits#2", Text = "Free: 2 GB per file." },
        ];

        Assert.True(Citations.CitedChunkContains("It is 250 GB [file-limits#0].", sources, "250 GB"));
        Assert.False(Citations.CitedChunkContains("It is 250 GB [file-limits#2].", sources, "250 GB"));
        Assert.False(Citations.CitedChunkContains("It is 250 GB.", sources, "250 GB"));
    }

    [Fact]
    public async Task Regression_set_shows_heading_chunks_finding_every_answer_where_small_windows_cut_facts()
    {
        var documents = new[] { FileLimits, SyncClient, Plans };
        var byHeading = await NewStore(documents);
        var windows = await RagStore.CreateAsync(new HashingEmbeddingGenerator());
        foreach (var document in documents)
        {
            await Ingestor.IngestAsync(windows, document, d => FixedChunker.Chunk(d.Markdown, size: 6, overlap: 0));
        }

        Assert.Equal(4, await RetrievalEval.HitsAtAsync(byHeading, Questions, k: 1));
        Assert.True(await RetrievalEval.HitsAtAsync(windows, Questions, k: 1) < 4);
    }

    // Optional tasks: these two tests pass until you start the optional TODO, then they check your work.

    [Fact]
    public async Task Optional_task_rag_client_grounds_the_call_and_reports_the_sources()
    {
        ScriptedChatClient model;
        ChatResponse response;
        try
        {
            var store = await NewStore(FileLimits, SyncClient);
            model = new ScriptedChatClient(_ => "Port 47613 [sync-client#0]");
            response = await new RagChatClient(model, store, k: 2).GetResponseAsync("Which UDP port does local discovery use");
        }
        catch (NotImplementedException)
        {
            return;
        }

        Assert.Equal(2, model.Requests[0].Count);
        Assert.Equal(ChatRole.System, model.Requests[0][0].Role);
        Assert.Contains("UDP port 47613", model.Requests[0][0].Text);
        var ids = Assert.IsType<string[]>(response.AdditionalProperties![RagChatClient.SourcesKey]);
        Assert.Equal("sync-client#0", ids[0]);
    }

    [Fact]
    public async Task Optional_task_rag_client_grounds_the_streaming_call_too()
    {
        ScriptedChatClient model;
        var text = new System.Text.StringBuilder();
        try
        {
            var store = await NewStore(FileLimits, SyncClient);
            model = new ScriptedChatClient(_ => "Port 47613");
            var rag = new RagChatClient(model, store, k: 2);
            await foreach (var update in rag.GetStreamingResponseAsync("Which UDP port does local discovery use"))
            {
                text.Append(update.Text);
            }
        }
        catch (NotImplementedException)
        {
            return;
        }

        Assert.Equal("Port 47613 ", text.ToString());
        Assert.Equal(ChatRole.System, model.Requests[0][0].Role);
        Assert.Contains("UDP port 47613", model.Requests[0][0].Text);
    }
}
