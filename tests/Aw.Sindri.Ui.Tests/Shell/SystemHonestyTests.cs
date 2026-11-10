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

    [Fact]
    public async Task The_headline_glyph_follows_the_headline_tone_not_the_pill()
    {
        var system = new SystemTestHarness();
        await system.Show(A.Status() with { Health = new HealthView("healthy", []) });
        Assert.Equal(StateGlyph.Ok, system.Vm.HeadlineGlyph);

        system.Server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, MissingIndex);
        await system.Vm.CodeIndex.Refresh(A.Record().Url, Token);

        Assert.True(system.Vm.ToneWarn);
        Assert.Equal(StateGlyph.Warning, system.Vm.HeadlineGlyph);
    }

    // #2554: a stalled projector was said four times on the System page. The page's own banner says it; the "Needs a look" list keeps what the banner doesn't.
    [Fact]
    public async Task The_needs_a_look_list_leaves_out_the_stall_the_banner_already_says()
    {
        var system = new SystemTestHarness("stalled");
        await system.Show(A.Status() with
        {
            Health = new HealthView("stalled", [new HealthProblemView("SND-VIEW-001", "Views are stalled at 337: run `sindri views rebuild`"), new HealthProblemView("SND-SVC-404", "Service broken failed 1 min ago")]),
        });

        Assert.Equal(["SND-SVC-404"], system.Vm.ProblemLines.Select(l => l.Code), StringComparer.Ordinal);
    }
}
