using System.Net;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>Closing a sheet opened from a strip row returns focus to that strip row (review run 1, a11y-13; UI spec §11.2 "Esc or back returns it to the row").</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class StripFocusReturnTests : FrameHost
{
    // The shared golden's only answerable strip row is a desk one: its protected-answer row, with a plain answer class, is the one a row sheet answers.
    private static RouteAnswer AnswerableOffTheDesk()
    {
        var list = GoldenFiles.Node("workflow-instances", "root");
        var rows = list["instances"]!.AsArray();
        var row = rows.Single(r => r!["answer"]?["class"]?.GetValue<string>() == "protected")!;
        row["answer"]!["class"] = "pause";
        _ = rows.Remove(row);
        rows.Insert(0, row);   // the strip shows the first few open rows in served order; this one is eighth
        return new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());
    }

    [Fact]
    public Task Esc_after_Answer_returns_focus_to_the_strip_row_not_the_Needs_you_row() => OnUi(async () =>
    {
        Server.InstancesBody = AnswerableOffTheDesk();
        var (window, shell) = await Connected(s => s.Strip.Rows.Any(r => r.CanAnswer && !r.AtDesk));
        var row = shell.Strip.Rows.First(r => r.CanAnswer && !r.AtDesk);   // a desk row's Answer opens the Needs-you inbox instead of a row sheet (#1057)
        var park = NeedsYouRows.Park("park:" + row.Address) with { Subject = new NeedsYouSubject("workflow", row.Address) };
        shell.ShowNeedsYou(new NeedsYouFeed([park], ""));
        Settle(window);
        var frame = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "StripRowFrame" && b.DataContext is StripRowViewModel vm && vm.Address == row.Address);
        Assert.True(frame.Focus(NavigationMethod.Tab));
        Settle(window);

        shell.AnswerStripRow(row.Address);
        Settle(window);
        shell.CloseSheet();
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        Assert.Same(frame, window.FocusManager?.GetFocusedElement());
    });
}
