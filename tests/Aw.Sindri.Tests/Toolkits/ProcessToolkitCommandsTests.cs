using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Toolkits;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>The command runner keeps standard output and standard error apart (agent-first PR 16): a manifest parses from output alone, a failure tail shows both.</summary>
[Lane(TestLane.Unit)]
public sealed class ProcessToolkitCommandsTests
{
    [Fact]
    public async Task Output_and_error_come_back_apart()
    {
        var commands = new ProcessToolkitCommands(new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, """{"name":"x"}""", "warn: default port")));

        var ran = await commands.Run("/tk", ["/tk/bin/x", "--manifest"], TestContext.Current.CancellationToken);

        Assert.Equal("""{"name":"x"}""", ran.Output);
        Assert.Equal("warn: default port", ran.Error);
    }
}
