using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The System overview says what its cards say: the headline folds in the Code index, and the Agents card counts working sessions.</summary>
[Lane(TestLane.Unit)]
public sealed class SystemHonestyTests
{
    private const string MissingIndex = """{"repos":[{"repo":"repo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}],"health":[]}""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_healthy_server_with_a_code_index_that_needs_a_look_does_not_say_everything_is_running()
    {
        var system = new SystemTestHarness();
        await system.Show(A.Status() with { Health = new HealthView("healthy", []) });
        Assert.Equal(Labels.AllRunning, system.Vm.Sentence);
        Assert.False(system.Vm.ToneWarn);

        system.Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, MissingIndex);
        await system.Vm.CodeIndex.Refresh(A.Record().Url, Token);

        Assert.Equal("Something needs a look: code index.", system.Vm.Sentence);
        Assert.True(system.Vm.ToneWarn);
        Assert.False(system.Vm.ToneError);
    }
}
