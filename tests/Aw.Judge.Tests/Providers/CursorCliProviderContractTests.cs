using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class CursorCliProviderContractTests : ModelProviderContract
{
    protected override IModelProvider Create() => Answering("send")!;   // why: this override never returns null

    protected override IModelProvider? Answering(string decision) =>
        With(new FakeProgram { StandardOutput = Questions.ResultText($$"""{"decision":"{{decision}}"}""") });

    protected override IModelProvider? Failing() => With(new FakeProgram { ExitCode = 1, StandardError = "boom" });

    protected override IModelProvider? Hanging() => With(new FakeProgram { Runs = TimeSpan.FromMinutes(1) });

    private static CursorCliProvider With(FakeProgram program) => new(new FakeProcessRunner().With("cursor-agent", program), new FakeInputRedactor());
}
