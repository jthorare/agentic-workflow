using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.Views.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>
/// A CommandControl keeps every button and text inside its own bounds at any host width (#3116): a long CLI in the copy block
/// wraps and Copy stays reachable, and a long accepted outcome stays beside the run button without leaving the control.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CommandControlWidthTests
{
    private const string LongCli = "sindri memory write <name> <text> [--tag <tag>] [--expires-at <time>]";

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CommandControlWidthTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static CommandRowView Row(string action, string cli) =>
        new(action, "desk", ["human"], Terminal: false, "POST", "/sindri/memory", cli, "ready", Destructive: false, ReadsViews: false, Shortcut: null);

    private static (Window Window, CommandControl Control) Show(ControlViewModel vm, double width)
    {
        var control = new CommandControl { DataContext = vm };
        var window = new Window { Content = control, Width = width, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, control);
    }

    private static T Part<T>(CommandControl control, string name)
        where T : Control =>
        control.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static double RightEdge(Control part, Control root) =>
        (part.TranslatePoint(new Point(part.Bounds.Width, 0), root) ?? throw new InvalidOperationException(part.Name + " is not under the control")).X;

    private static ControlViewModel CopyOnly(string cli) =>
        new(Row("WriteMemory", cli), send: null, new FakeClipboard(), new FakeTimeProvider(A.Noon));

    [Fact]
    public async Task A_long_cli_wraps_and_keeps_Copy_inside_a_narrow_host()
    {
        var (controlWidth, copyRight, copyWidth, cliRight) = await OnUi(() =>
        {
            var (window, control) = Show(CopyOnly(LongCli), 320);
            var copy = Part<Button>(control, "BlockCopy");
            var result = (control.Bounds.Width, RightEdge(copy, control), copy.Bounds.Width, RightEdge(Part<TextBlock>(control, "CliText"), control));
            window.Close();
            return result;
        });

        Assert.True(copyWidth > 0, string.Create(CultureInfo.InvariantCulture, $"Copy has width {copyWidth}"));
        Assert.True(copyRight <= controlWidth, string.Create(CultureInfo.InvariantCulture, $"Copy ends at {copyRight}, past the control's {controlWidth}"));
        Assert.True(cliRight <= controlWidth, string.Create(CultureInfo.InvariantCulture, $"the CLI text ends at {cliRight}, past the control's {controlWidth}"));
    }

    [Fact]
    public async Task The_wrapped_CLI_text_carries_the_full_command_as_its_tooltip()
    {
        var (tip, shown) = await OnUi(() =>
        {
            var (window, control) = Show(CopyOnly(LongCli), 320);
            var text = Part<TextBlock>(control, "CliText");
            var result = (ToolTip.GetTip(text), text.Text);
            window.Close();
            return result;
        });

        Assert.Equal(LongCli, tip);
        Assert.Equal(LongCli, shown);
    }

    [Fact]
    public async Task A_short_cli_keeps_Copy_right_after_the_text_in_a_wide_host()
    {
        var copyLeft = await OnUi(() =>
        {
            var (window, control) = Show(CopyOnly("sindri item close <item>"), 1000);
            var copy = Part<Button>(control, "BlockCopy");
            var left = RightEdge(copy, control) - copy.Bounds.Width;
            window.Close();
            return left;
        });

        Assert.True(copyLeft < 400, string.Create(CultureInfo.InvariantCulture, $"Copy starts at {copyLeft}"));
    }

    [Fact]
    public async Task A_long_outcome_stays_inside_a_narrow_host()
    {
        var (controlWidth, outcomeRight, outcome) = await OnUi(() =>
        {
            var vm = new ControlViewModel(
                Row("ApplyCommand:pause-all-running-services-and-wait-for-every-session-to-settle-down", "sindri services pause all"),
                _ => Task.FromResult<Result<CommandAcceptedView>>(new CommandAcceptedView("cmd-1", 1)),
                new FakeClipboard(),
                new FakeTimeProvider(A.Noon));
            var (window, control) = Show(vm, 320);
            ControlAutomationPeer.CreatePeerForElement(control).GetProvider<IInvokeProvider>()?.Invoke();
            Dispatcher.UIThread.RunJobs();
            var text = Part<TextBlock>(control, "OutcomeText");
            var result = (control.Bounds.Width, RightEdge(text, control), vm.Outcome);
            window.Close();
            return result;
        });

        Assert.NotEmpty(outcome);
        Assert.True(outcomeRight <= controlWidth, string.Create(CultureInfo.InvariantCulture, $"the outcome ends at {outcomeRight}, past the control's {controlWidth}"));
    }
}
