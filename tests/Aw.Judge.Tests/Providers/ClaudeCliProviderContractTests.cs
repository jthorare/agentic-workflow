using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ClaudeCliProviderContractTests : ModelProviderContract
{
    protected override IModelProvider Create() => With(new FakeProgram { StandardOutput = Questions.ClaudeStructured("""{"decision":"send"}""") });

    protected override IModelProvider? Answering(string decision) =>
        With(new FakeProgram { StandardOutput = Questions.ClaudeStructured($$"""{"decision":"{{decision}}"}""") });

    protected override IModelProvider? Failing() => With(new FakeProgram { ExitCode = 1, StandardError = "boom" });

    protected override IModelProvider? Hanging() => With(new FakeProgram { Runs = TimeSpan.FromMinutes(1) });

    private static ClaudeCliProvider With(FakeProgram program) => new(new FakeProcessRunner().With("claude", program));
}
