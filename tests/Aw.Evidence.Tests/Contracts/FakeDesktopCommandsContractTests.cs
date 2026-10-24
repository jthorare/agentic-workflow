using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeDesktopCommandsContractTests : DesktopCommandsContract
{
    private const string Tool = "scripts/tool.sh";

    protected override Task<DesktopCommandsUnderTest> ArrangeAsync(CliProgram program) =>
        Task.FromResult(new DesktopCommandsUnderTest(
            new FakeDesktopCommands().When(static _ => true, _ => new CommandResult(program.ExitCode, program.Stdout, string.Empty)), Tool, "/work/repo"));

    protected override Task<DesktopCommandsUnderTest> ArrangeMissingAsync()
    {
        var fake = new FakeDesktopCommands();
        _ = fake.WontStart.Add(Tool);
        return Task.FromResult(new DesktopCommandsUnderTest(fake, Tool, "/work/repo"));
    }
}
