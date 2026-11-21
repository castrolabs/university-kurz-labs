using System.Text.Json;
using Microsoft.Extensions.AI;

namespace StructuredOutputLab.Tests;

public sealed class StructuredOutputTests
{
    private const string TicketJson = """
        {"customer":"Maria Souza","quantity":3,"priority":"Urgent","orderNumber":"ORD-88213",
         "products":["USB-C dock (x2)","27\" monitor stand (x1)"],
         "shipTo":{"city":"Porto","country":"Portugal"},"orderDate":"2026-10-12"}
        """;

    private static ChatResponseFormatJson FormatSentTo(ScriptedChatClient client) =>
        Assert.IsType<ChatResponseFormatJson>(client.Options[0]!.ResponseFormat);

    // --- GetResponseAsync<T> and the schema ---

    [Fact]
    public async Task ShouldTurnTheReplyIntoATypedTicketAndAskForTheSchema()
    {
        var model = new ScriptedChatClient(TicketJson);

        Ticket ticket = await TicketExtractor.ExtractAsync(model, "Hi, I'm Maria...");

        Assert.Equal("Maria Souza", ticket.Customer);
        Assert.Equal(3, ticket.Quantity);
        Assert.Equal(Priority.Urgent, ticket.Priority);                  // "Urgent" text became the enum
        Assert.Equal(new DateOnly(2026, 10, 12), ticket.OrderDate);
        Assert.Equal(new Address("Porto", "Portugal"), ticket.ShipTo);
        Assert.Equal(2, ticket.Products.Count);
        Assert.NotNull(FormatSentTo(model).Schema);                      // the type became the request format
        Assert.Equal(0f, model.Options[0]!.Temperature);
    }

    [Fact]
    public async Task ShouldReadANullForAMissingFactIntoTheNullableProperty()
    {
        var model = new ScriptedChatClient(TicketJson.Replace("\"ORD-88213\"", "null"));

        Ticket ticket = await TicketExtractor.ExtractAsync(model, "no order number here");

        Assert.Null(ticket.OrderNumber);
    }

    [Fact]
    public void ShouldKeepNullableAndNonNullableFieldsRequiredAndDescribeTheEnum()
    {
        // string? is a union with null but it is still "required"; nothing here forbids extra properties.
        Assert.Equal(
            ["customer", "quantity", "priority", "orderNumber", "products", "shipTo", "orderDate"],
            SchemaInspector.Required<Ticket>());

        JsonElement schema = ChatResponseFormat.ForJsonSchema<Ticket>().Schema!.Value;
        var props = schema.GetProperty("properties");
        Assert.Equal(["Low", "Normal", "Urgent"], props.GetProperty("priority").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("date", props.GetProperty("orderDate").GetProperty("format").GetString());
        Assert.Equal("Customer full name", props.GetProperty("customer").GetProperty("description").GetString());
        Assert.Contains("null", props.GetProperty("orderNumber").GetProperty("type").EnumerateArray().Select(e => e.GetString()));
        Assert.False(schema.TryGetProperty("additionalProperties", out _));
    }

    [Fact]
    public void ShouldTurnDataAnnotationsIntoSchemaKeywordsButNotIntoRequired()
    {
        JsonElement schema = ChatResponseFormat.ForJsonSchema<Order>().Schema!.Value;

        var quantity = schema.GetProperty("properties").GetProperty("quantity");
        Assert.Equal(1, quantity.GetProperty("minimum").GetInt32());
        Assert.Equal(100, quantity.GetProperty("maximum").GetInt32());
        Assert.Equal(["orderNumber"], SchemaInspector.Required<Order>());   // only `required` members
    }

    // --- arrays, primitives and enums are wrapped in "data" ---

    [Fact]
    public async Task ShouldWrapArraysAndPrimitivesInADataObject()
    {
        var colors = new ScriptedChatClient("""{ "data": [ "red", "yellow", "blue" ] }""");
        Assert.Equal(["red", "yellow", "blue"], await Primitives.PrimaryColorsAsync(colors));
        var root = FormatSentTo(colors).Schema!.Value;
        Assert.Equal("object", root.GetProperty("type").GetString());                  // never an array at the root
        Assert.Equal("array", root.GetProperty("properties").GetProperty("data").GetProperty("type").GetString());

        Assert.Equal(8, await Primitives.CountWheelsAsync(new ScriptedChatClient("""{ "data": 8 }""")));

        // A fake or a cache that keeps the bare value breaks the contract.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Primitives.CountWheelsAsync(new ScriptedChatClient("42")));
    }

    [Fact]
    public async Task ShouldClassifyIntoAnEnumWrappedInData()
    {
        var model = new ScriptedChatClient("""{"data":"Shipping"}""");

        Category category = await Classifier.ClassifyAsync(model, "My package never arrived");

        Assert.Equal(Category.Shipping, category);
        var data = FormatSentTo(model).Schema!.Value.GetProperty("properties").GetProperty("data");
        Assert.Equal(["Billing", "Shipping", "Technical"], data.GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
    }

    // --- when the text is not the JSON you expected ---

    [Fact]
    public async Task ShouldReportEachKindOfBadReplyWithoutThrowing()
    {
        (string Reply, bool Ok, string? ErrorPart)[] cases =
        [
            ("", false, "InvalidOperationException: The response did not contain JSON"),
            ("Sure! Here is the JSON: {\"label\":\"positive\",\"score\":4}", false, "JsonException: 'S' is an invalid start of a value"),
            ("```json\n{\"label\":\"positive\",\"score\":4}\n```", false, "JsonException: '`' is an invalid start of a value"),
            ("null", false, "InvalidOperationException: The deserialized response is null"),
            ("""{"label":"positive","score":"four"}""", false, "JsonException: The JSON value could not be converted to"),
            ("""{"label":"positive"}""", true, null),                                      // missing member: default, no error
            ("""{"label":"first","score":1} {"label":"second","score":2}""", true, null),   // the first value wins
        ];

        foreach (var (reply, ok, errorPart) in cases)
        {
            var outcome = await SafeParse.TryExtractAsync<Sentiment>(new ScriptedChatClient(reply));

            Assert.True(ok == outcome.Ok, $"reply: {reply}");
            if (errorPart is not null)
            {
                Assert.Contains(errorPart, outcome.Error);
            }
        }

        Assert.Equal(0, (await SafeParse.TryExtractAsync<Sentiment>(new ScriptedChatClient("""{"label":"positive"}"""))).Value!.Score);
        Assert.Equal("first", (await SafeParse.TryExtractAsync<Sentiment>(new ScriptedChatClient("""{"label":"first","score":1} {"label":"second","score":2}"""))).Value!.Label);
    }

    [Fact]
    public async Task ShouldRefuseAReplyThatMissesARequiredMember()
    {
        var outcome = await SafeParse.TryExtractAsync<Order>(new ScriptedChatClient("""{"quantity":250}"""));

        Assert.False(outcome.Ok);
        Assert.Contains("missing required properties including: 'orderNumber'", outcome.Error);
    }

    // --- deserialization is not validation, and a bounded retry ---

    [Fact]
    public async Task ShouldCatchWhatTheTypeAloneDoesNotCheck()
    {
        var outcome = await SafeParse.TryExtractAsync<Order>(new ScriptedChatClient("""{"orderNumber":"ORD-1","quantity":250}"""));

        Assert.True(outcome.Ok);   // it parsed...
        Assert.Equal(["The field Quantity must be between 1 and 100."], OrderValidator.Validate(outcome.Value!));
        Assert.Empty(OrderValidator.Validate(new Order { OrderNumber = "ORD-1", Quantity = 25 }));
    }

    [Fact]
    public async Task ShouldRetryWithTheErrorFedBackAndStopAtTheLimit()
    {
        var recovering = new ScriptedChatClient(
            "Sorry, here it is: {",
            """{"orderNumber":"ORD-1","quantity":250}""",
            """{"orderNumber":"ORD-1","quantity":25}""");

        var ok = await RetryingExtractor.ExtractAsync<Order>(recovering, "order ORD-1, we need 25 units.", maxAttempts: 3);

        Assert.Equal(25, ok.Value!.Quantity);
        Assert.Equal(3, ok.Attempts);
        Assert.Equal(3, recovering.Calls);
        Assert.Equal(5, recovering.Requests[^1].Count);   // user, reply, correction, reply, correction
        Assert.Contains("The field Quantity must be between 1 and 100.", recovering.Requests[^1][^1].Text);

        var hopeless = new ScriptedChatClient("nope");
        var failed = await RetryingExtractor.ExtractAsync<Order>(hopeless, "order ORD-1", maxAttempts: 3);

        Assert.Null(failed.Value);
        Assert.Equal(3, failed.Attempts);
        Assert.Equal(3, hopeless.Calls);   // bounded: it gave up
    }

    // --- serializer options and streaming ---

    [Fact]
    public void ShouldReadTheReplyWithTheOptionsTheSchemaWasBuiltFrom()
    {
        // Plain Deserialize: camelCase never matches the record, and nothing throws.
        Ticket? silent = JsonSerializer.Deserialize<Ticket>(TicketJson);
        Assert.Null(silent!.Customer);
        Assert.Equal(0, silent.Quantity);

        // Web options do not read "Urgent" into the enum.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Ticket>(TicketJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Ticket ticket = JsonText.Parse<Ticket>(TicketJson)!;
        Assert.Equal("Maria Souza", ticket.Customer);
        Assert.Equal(Priority.Urgent, ticket.Priority);
    }

    [Fact]
    public async Task ShouldParseAStreamOnlyAfterItEnded()
    {
        var model = new ScriptedChatClient(TicketJson);

        var chunks = new List<string>();
        await foreach (var update in model.GetStreamingResponseAsync("x")) { chunks.Add(update.Text); }
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Ticket>(chunks[0], AIJsonUtilities.DefaultOptions));   // a chunk is not JSON

        Ticket ticket = await StreamingExtractor.ExtractAsync<Ticket>(new ScriptedChatClient(TicketJson), "Extract the ticket");

        Assert.Equal("Maria Souza", ticket.Customer);
        Assert.Equal(new DateOnly(2026, 10, 12), ticket.OrderDate);
    }
}
