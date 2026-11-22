using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3702: a Starting run row that got no run row from the server for 10 s says "The run didn't start." and offers Retry, both found by AutomationId, visible and enabled.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsRunStartFailedFramesTests : FrameHost
{
    private static T ById<T>(Window window, string id)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().First(c => AutomationProperties.GetAutomationId(c) == id);

    [Fact]
    public Task A_run_that_never_landed_shows_the_did_not_start_text_and_an_enabled_Retry_button() => OnUi(async () =>
    {
        Server.WorkflowsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, Golden("workflows", "list-two").ToJsonString());
        Server.WorkflowAnswers[Slug] = RouteAnswer.Api("workflows-slug", "found");
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Rows.Count > 0, "the runs never showed");
        await shell.Workflows.RunNow(CancellationToken.None);
        shell.Workflows.Back();   // the runs list, where the placeholder row is
        Clock.Advance(TimeSpan.FromSeconds(10));
        await Until(window, () => shell.Workflows.Runs.Rows.Any(r => r.DidNotStart), "the placeholder never timed out");

        var text = ById<TextBlock>(window, "RunDidNotStart");
        var retry = ById<Button>(window, "RunStartRetry");

        Assert.Equal("The run didn't start.", text.Text);
        Assert.True(text.IsEffectivelyVisible && retry.IsEffectivelyVisible && retry.IsEnabled);
        Assert.Equal("Retry", retry.Content);
    });
}
