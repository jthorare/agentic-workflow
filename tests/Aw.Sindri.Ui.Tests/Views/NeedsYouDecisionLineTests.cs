using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The decision sheet is bound once, before any decision shows: its Copy button and its line's name follow the decision shown (review run 5, r5-0).</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouDecisionLineTests : IDisposable
{
    private readonly List<Window> _windows = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _stop.Dispose();

    private async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(NeedsYouDecisionLineTests).Assembly)
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

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Avalonia.Visual within, string name)
        where T : Control => within.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static NeedsYouEntry Pending(NeedsYouEntry row) =>
        row with { Actions = [.. row.Actions.Select(a => a with { Readiness = ActionReadiness.Pending })] };

    [Fact]
    public Task The_decision_sheet_shows_Copy_and_names_the_line_after_the_decision_shown() => OnUi(async () =>
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        var window = new ShellWindow { DataContext = shell };
        _windows.Add(window);
        window.Show();
        _ = shell.Run(_stop.Token);
        for (var i = 0; i < 300 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(10, Token);
            Settle(window);
        }

        var second = Pending(NeedsYouRows.Decision("decision:DEC-2") with { Subject = new NeedsYouSubject("decision", "DEC-2") });
        shell.ShowNeedsYou(new NeedsYouFeed([Pending(NeedsYouRows.Decision()), second], ""));

        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        Settle(window);
        var sheet = Named<Control>(window, "DecisionSheet");
        Assert.True(Named<Button>(sheet, "DecisionDecideCopy").IsEffectivelyVisible);
        Assert.StartsWith("DEC-1: ", AutomationProperties.GetName(Named<Control>(sheet, "DecisionDecideLine")), StringComparison.Ordinal);

        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-2"));
        Settle(window);
        Assert.StartsWith("DEC-2: ", AutomationProperties.GetName(Named<Control>(sheet, "DecisionDecideLine")), StringComparison.Ordinal);
        Assert.Contains("DEC-2", AutomationProperties.GetName(Named<Button>(sheet, "DecisionDecideCopy")), StringComparison.Ordinal);
    });
}
