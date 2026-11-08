using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.Tests.Takeover;

/// <summary>What a takeover's outcome says, and when the attach command is shown.</summary>
[Lane(TestLane.Unit)]
public sealed class ViewerWordsTests
{
    private static readonly string[] _attach = ["tmux", "attach"];

    [Fact]
    public void No_viewer_and_no_command_says_the_server_did_not_say() =>
        Assert.Equal((Labels.TakeoverNoViewer, string.Empty), ViewerWords.TakeoverOutcome(viewer: null, attach: null));

    [Fact]
    public void No_viewer_with_a_command_asks_the_person_to_run_it() =>
        Assert.Equal((Labels.AttachRunThis, "tmux attach"), ViewerWords.TakeoverOutcome(viewer: null, _attach));

    [Fact]
    public void ShowsCommand_needs_a_command()
    {
        Assert.False(ViewerWords.ShowsCommand(viewer: null, attach: null));
        Assert.False(ViewerWords.ShowsCommand(viewer: null, attach: []));
        Assert.True(ViewerWords.ShowsCommand(viewer: null, _attach));
    }
}
