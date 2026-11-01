using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>A Jobs row in a narrow window (#1728): the key under the title stays inside the job column and never draws over the Status chip.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class JobRowLayoutTests : FrameHost
{
    private const string Key = "map-no-answer";

    [Fact]
    public Task At_1000_px_the_key_stays_inside_the_job_column() => OnUi(async () =>
    {
        var list = Golden("jobs", "running");
        var job = list["jobs"]![0]!;
        job["kind"] = "BuildContextMap";
        job["key"] = Key;
        Server.JobsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        Server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", "running");
        var (window, shell) = await Connected(width: 1000, height: 1000);
        shell.GoTo(PageId.Jobs);
        await Until(window, () => shell.Jobs.Drawn > 0, "the Jobs page never read the list");
        Settle(window);

        var grid = Named<Grid>(window, "JobRowGrid");
        var key = grid.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == Key);
        var chip = Named<Border>(window, "JobRowChip");

        var keyRight = key.TranslatePoint(default, window)!.Value.X + key.Bounds.Width;
        var chipLeft = chip.TranslatePoint(default, window)!.Value.X;
        Assert.True(keyRight <= chipLeft + 0.5, "the key draws over the Status column at 1000 px");
    });
}
