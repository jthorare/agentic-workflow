using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.Views.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>A command to copy keeps its Copy button inside the card it sits in, however long the command is (UX-pass B-10).</summary>
[Lane(TestLane.Integration)]
public sealed class CommandControlLayoutTests
{
    private const string LongCli = "sindri launch init [--viewer <name>] [--force] [--start-command <command>] [--workdir <directory>] [--terminal <terminal>] [--no-open]";

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CommandControlLayoutTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task A_long_command_wraps_and_its_Copy_button_stays_inside_a_440_px_card()
    {
        var (copyRight, cardWidth, copyEnabled) = await OnUi(() =>
        {
            var row = new CommandRowView("LaunchInit", "desk", ["human+terminal"], Terminal: true, Method: null, Route: null, LongCli, "cli-only:terminal", Destructive: false, ReadsViews: false, Shortcut: null);
            var control = new CommandControl { DataContext = new ControlViewModel(row, send: null, new FakeClipboard(), new FakeTimeProvider(A.Noon)) };
            var card = new Border { Width = 440, Child = control };
            var window = new Window { Content = new StackPanel { Children = { card } }, Width = 800, Height = 400 };
            window.Show();
            Settle(window);
            var copy = control.GetVisualDescendants().OfType<Button>().First(b => b.Name == "BlockCopy");
            var result = (copy.TranslatePoint(new Point(copy.Bounds.Width, 0), card)!.Value.X, card.Bounds.Width, copy.IsEffectivelyEnabled); // why: both are in the same tree
            window.Close();
            return result;
        });

        Assert.True(copyEnabled);
        Assert.True(copyRight <= cardWidth + 0.5, "Copy ends at x=" + copyRight.ToString(CultureInfo.InvariantCulture) + " in a card " + cardWidth.ToString(CultureInfo.InvariantCulture) + " px wide");
    }
}
