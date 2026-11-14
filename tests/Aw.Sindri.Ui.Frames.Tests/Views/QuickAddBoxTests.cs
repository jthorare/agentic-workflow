using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The quick-add box under the headless platform (todo spec §3.2, §3.5): it opens over the page, draws the preview line and names itself as a dialog.</summary>
[Lane(TestLane.Integration)]
public sealed class QuickAddBoxTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(QuickAddBoxTests).Assembly)
            .Dispatch(async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    await _stop.CancelAsync();
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 3000 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(1, Token);
            Settle(window);
        }

        Assert.Equal(ShellState.Normal, shell.State);
        return (window, shell);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Window window, string name)
        where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    [Fact]
    public Task The_box_opens_under_the_top_bar_at_560_px_with_a_labelled_dialog_and_field() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        Assert.True(shell.PressQuickAdd());
        Settle(window);

        var card = Named<Border>(window, "QuickAddCard");
        var field = Named<TextBox>(window, "QuickAddField");
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal(560, card.Bounds.Width);
        Assert.True(card.TranslatePoint(new Point(0, 0), window)!.Value.Y >= Named<Control>(window, "TopBar").Bounds.Height);
        Assert.Equal(("New todo", "New todo"), (AutomationProperties.GetName(card), AutomationProperties.GetName(field)));
        Assert.Equal(AutomationControlType.Window, AutomationProperties.GetControlTypeOverride(card));
    });

    [Fact]
    public Task The_preview_line_follows_what_is_typed_and_is_a_polite_live_region() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _ = shell.PressQuickAdd();
        Settle(window);

        shell.QuickAdd.Text = "check flaky test fri";
        Settle(window);

        // The shell's clock is A.Noon, Sunday 2026-10-11, so "fri" is the 16th.
        var preview = Named<TextBlock>(window, "QuickAddPreview");
        Assert.Equal(("Due Fri 10-16", AutomationLiveSetting.Polite), (preview.Text, AutomationProperties.GetLiveSetting(preview)));
    });

    [Fact]
    public Task Closing_the_box_hides_it_again() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        _ = shell.PressQuickAdd();
        Settle(window);

        shell.QuickAdd.Close();
        Settle(window);

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Border>(), b => b.Name == "QuickAddCard" && b.IsEffectivelyVisible);
    });
}
