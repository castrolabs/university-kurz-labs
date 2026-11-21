# Agent Framework: Agents, Sessions and Tools

## Goal

Put a Microsoft Agent Framework agent on top of an `IChatClient` and check, from the outside, what it sends to the model: where the instructions go, what turn 2 carries, what a serialized session holds, what happens to concurrent runs, who runs the tool loop, how approval pauses a run, what middleware sees and what a context provider leaves in the history.

## Prerequisites

- `IChatClient` and `AIFunction` (see the Microsoft.Extensions.AI and tool-calling lessons)
- `Microsoft.Agents.AI` 1.26.0 (`AsAIAgent`, `AIAgent`, `AgentSession`, `ChatClientAgentOptions`, `AIContextProvider`)
- .NET 10 SDK

The sibling lab `agent-framework-workflows-and-orchestration` covers workflow graphs: nothing of it is repeated here.

## Task

The tests use `ScriptedChatClient`, a deterministic `IChatClient`: it answers "reply 1", "reply 2", ... (or from a script that can ask for tool calls), and records every request and every `ChatOptions` it receives, so each claim is a count or a list of messages. There is no network and no Ollama.

Packages: `Microsoft.Agents.AI` 1.26.0 (stable), `Microsoft.Extensions.AI` 10.11.0.

The services `WeatherService`, `BankService` and `AccountService`, and the `CustomerContext` provider are given.

## Instructions

Complete the following TODOs:

- TODO-00: `Agents.CreateHelper`: `AsAIAgent` with instructions and name.
- TODO-01: `Conversation.AskAllAsync`: one session across the turns, or none.
- TODO-02: `Conversation.SerializeAsync` and `RestoreAsync`: save a session as JSON and continue it on a new agent.
- TODO-03: `SerializedAgent.RunAsync`: a per-session lock.
- TODO-04: `Agents.WithTools`: the tool loop is the agent's, not yours.
- TODO-05: `Agents.WithApproval`: `ApprovalRequiredAIFunction`.
- TODO-06: `Approvals.RunWithApprovalAsync`: resume a paused run on the same session.
- TODO-07: `Agents.WithPolicy`: run middleware and function-calling middleware that blocks one tool.
- TODO-08: `Agents.WithCustomerContextFiltered`: stop injected memory from piling up in the history.

Run the tests until they all pass.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

## Using Ollama instead of the fake

Nothing in `src/` depends on the provider. To try a real model, add `OllamaSharp` to the test project (`dotnet add tests/AgentLab.Tests package OllamaSharp --version 5.5.0`) and replace the fake where a test builds its client with `new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3:4b-instruct")`. The scripted answers ("reply 1") and exact message counts for tool calls will no longer hold.

## Bonus (Optional)

The two tests for these tasks pass until you start them, then they check your work.

- TODO-09 (optional): `Agents.WithSpecialist`: an agent used as a tool of another agent. Each model call is counted on its own client.
- TODO-10 (optional): `Agents.WithOwnPipeline`: a custom client stack with `UseProvidedChatClientAsIs`. Without `UseFunctionInvocation` the tool call comes back to you and nothing runs.
