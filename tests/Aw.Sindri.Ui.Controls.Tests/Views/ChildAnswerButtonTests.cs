using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.NeedsYou;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>"Answer in the child" in the rows, read at the automation peers (where AT-SPI reads): a stop in the control view whose name leads with the drawn words.</summary>
[Lane(TestLane.Integration)]
public sealed class ChildAnswerButtonTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ChildAnswerButtonTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    // What a screen reader reaches walking the automation tree down from the row, not a peer made for the control by hand.
    private static (string[] Controls, bool Lock) InRow(Control row)
    {
        var window = new Window { Content = row, Width = 1300, Height = 240 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        List<(string Name, AutomationControlType Type)> tree;
        try
        {
            tree = Heard.Tree(row);
        }
        finally
        {
            window.Close();
        }

        string[] buttons = [.. tree.Where(n => n.Type == AutomationControlType.Button).Select(n => n.Name)];
        return (buttons, Array.Exists([.. tree], n => n.Name.Contains("protected", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task A_workflows_row_has_the_control_with_its_name_and_no_lock()
    {
        var served = FixtureInstances.Summary("escalated-child");

        var heard = await OnUi(() => InRow(new InstanceRow { DataContext = new InstanceRowViewModel(served, new FakeClipboard(), new FakeTimeProvider(A.Noon)) }));

        Assert.Contains(Labels.AnswerInChildName(served.Answer!.Child!), heard.Controls, StringComparer.Ordinal);
        Assert.StartsWith(Labels.AnswerInChild, heard.Controls.First(n => n.StartsWith(Labels.AnswerInChild, StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.False(heard.Lock);
    }

    [Fact]
    public async Task A_strip_row_has_the_control_with_its_name_and_no_lock()
    {
        var served = FixtureInstances.Summary("parked-mirrored-budget");

        var heard = await OnUi(() => InRow(new StripRow { DataContext = new StripRowViewModel(served, new FakeClipboard(), new FakeTimeProvider(A.Noon)) }));

        Assert.Contains(Labels.AnswerInChildName(served.Answer!.Child!), heard.Controls, StringComparer.Ordinal);
        Assert.False(heard.Lock);
    }

    [Fact]
    public async Task A_needs_you_row_reaches_the_screen_reader_with_the_one_control_and_no_answer_button()
    {
        var row = ChildAnswerRows.Row("child");

        var heard = await OnUi(() => InRow(new NeedsYouRow { DataContext = row }));

        _ = Assert.Single(heard.Controls, n => n.StartsWith(Labels.AnswerInChild, StringComparison.Ordinal));
        Assert.DoesNotContain(heard.Controls, n => !n.StartsWith(Labels.AnswerInChild, StringComparison.Ordinal) && !n.StartsWith("Open", StringComparison.Ordinal));
    }
}
