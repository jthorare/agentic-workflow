using System.Net;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A resolved question shows the engine's files on the page (#2974, slice 1).</summary>
[Lane(TestLane.Integration)]
public sealed class ContextAnswerFilesFramesTests : FrameHost
{
    private const string Path = "src/Meta/Aw.Meta.Host/Commands/LaneRecycleCommand.cs";

    [Fact]
    public Task A_resolved_question_shows_its_file_list_on_the_page() => OnUi(async () =>
    {
        Server.WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.InternalServerError, "{}");
        Server.QueryAnswer = _ => new RouteAnswer(HttpStatusCode.OK, """
            {"anchors":["thing:lanes"],"suggestedAnchors":[],"sections":[{"name":"code","items":[{"kind":"code","node":"code:src/Meta/Aw.Meta.Host/Commands/LaneRecycleCommand.cs","title":"x","text":"implements feature:meta","why":["implements"]}]}]}
            """);
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Context);
        shell.Context.AnchorText = "How does aw-meta lane-recycle type into a pane?";
        await shell.Context.Submit(Token);
        Settle(window);

        Assert.True(Visible(window, "ContextAnswerFiles"));
        Assert.Equal(Path, shell.Context.AnswerFiles[0].Path);
        Assert.Equal(shell.Context.StageMessage, Named<TextBlock>(window, "ContextStageMessage").Text);   // the default map draws nodes: no sentence, and the control is bound to it
    });
}
