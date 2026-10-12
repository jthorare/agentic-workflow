using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class CodexCliProviderContractTests : ModelProviderContract
{
    protected override IModelProvider Create() => Answering("send")!;   // why: this override never returns null

    protected override IModelProvider? Answering(string decision) =>
        With(new FakeProgram { StandardOutput = Message($$"""{"decision":"{{decision}}"}""") + "\n" });

    protected override IModelProvider? Failing() => With(new FakeProgram { ExitCode = 1, StandardError = "boom" });

    protected override IModelProvider? Hanging() => With(new FakeProgram { Runs = TimeSpan.FromMinutes(1) });

    private static string Message(string text) =>
        new JsonObject { ["type"] = "item.completed", ["item"] = new JsonObject { ["type"] = "agent_message", ["text"] = text } }.ToJsonString();

    private static CodexCliProvider With(FakeProgram program) => new(new FakeProcessRunner().With("codex", program));
}
