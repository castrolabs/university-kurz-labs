# Agent Framework: Agents, Sessions and Tools (Solution)

Official solution. All tests pass, including the two optional ones.

## What to compare with your own code

- `CreateHelper`: instructions go into `ChatOptions.Instructions` on every call and never into the history, which lives in the session (`InMemoryChatHistoryProvider`).
- `AskAllAsync`: the agent resends the whole history every turn, so input tokens grow; without a session each call stands alone.
- `SerializeAsync` / `RestoreAsync`: the JSON holds the messages but not instructions or tools: restore with an agent configured the same way. In 1.26.0 only `SerializeSessionAsync` exists.
- `SerializedAgent`: concurrent runs on one session do not throw, they interleave. The test holds the first model call while the second would finish first; without the lock the history comes out reordered. In the original lesson a reply vanished in 13 of 20 runs; with this fake the loss did not reproduce (the order broke, nothing was lost).
- `WithTools`: the agent adds its own `FunctionInvokingChatClient`; the model is called twice and the tool runs once.
- `WithApproval` / `RunWithApprovalAsync`: the first run ends with a `ToolApprovalRequestContent` and no tool run; the approval goes back as a new run on the same session.
- `WithPolicy`: function-calling middleware only works on agents that use `FunctionInvokingChatClient`, such as `ChatClientAgent`. Blocking returns a string result without calling `next`.
- `WithCustomerContextFiltered`: injected messages are stored by default ("may become permanent additions"), so a fact injected every turn piles up (6 messages after two turns). Keeping only `External` messages in storage leaves 4; the model still receives the memory each turn.
- `WithSpecialist` (optional): the parent's usage contains its own calls only; the specialist's tokens are on the specialist's client.
- `WithOwnPipeline` (optional): `UseProvidedChatClientAsIs = true` adds nothing, so a custom stack needs `UseFunctionInvocation()`, or the `FunctionCallContent` is returned as the answer.

Docs only, not run here: Foundry hosting, A2A, durable agents, `UseToolApproval` rules, `TextSearchProvider` and `ChatHistoryMemoryProvider`.

## Running the Lab

```bash
dotnet test
```
