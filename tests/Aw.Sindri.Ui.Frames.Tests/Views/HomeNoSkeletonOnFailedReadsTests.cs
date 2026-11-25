using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#2921, the prevention: with every read behind Home answering an error, no Home card is left on its loading skeleton.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeNoSkeletonOnFailedReadsTests : FrameHost
{
    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task No_home_card_keeps_a_skeleton_after_its_read_fails(double width) => OnUi(async () =>
    {
        FailEveryHomeRead();

        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Home);
        await Until(window, () => !shell.Todo.IsLoading && !shell.Queue.IsLoading, "the Home reads never settled");
        await Task.Delay(300, Token);   // the other cards' failed reads land on their own tasks
        Settle(window);

        var home = window.GetVisualDescendants().OfType<HomePage>().Single(h => h.IsEffectivelyVisible);
        var left = home.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Classes.Contains("skeleton")).ToList();
        Assert.True(left.Count == 0, string.Create(CultureInfo.InvariantCulture, $"Home kept {left.Count} skeleton element(s) after every read failed, inside: {Owners(left)}"));
    });

    private void FailEveryHomeRead()
    {
        var failed = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TEST-500", "boom");
        Server.NeedsYouView = failed;       // the Needs you card
        Server.PendingDecisions = failed;
        Server.TodosAnswer = failed;        // the Todo card
        Server.TodoNoteAnswer = failed;     // its From elsewhere part
        Server.QueueView = failed;          // Running and Queued
        Server.ReviewsAnswer = failed;      // the PRs line
        Server.LinearIssuesAnswer = failed; // the Linear line
        Server.LinearSinceAnswer = failed;
    }

    private static string Owners(IEnumerable<Control> skeletons) => string.Join(
        ", ",
        skeletons.Select(s => s.GetVisualAncestors().OfType<Control>().FirstOrDefault(a => !string.IsNullOrEmpty(a.Name))?.Name ?? s.Parent?.GetType().Name ?? "?").Distinct(StringComparer.Ordinal));
}
