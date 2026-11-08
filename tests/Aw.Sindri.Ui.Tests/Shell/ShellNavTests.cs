using System.ComponentModel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The top bar's tabs keep their identity when a count changes, so the current page's highlight stays (proof B4).</summary>
[Lane(TestLane.Unit)]
public sealed class ShellNavTests
{
    private static readonly string[] _views = ["extra-p11-running", "empty", "fixture"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task The_queue_tab_stays_selected_across_count_changes() => UiThread.Run(async () =>
    {
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var server = new FakeSindriApi { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };
        var clock = new FakeTimeProvider(A.Noon);
        var shell = new ShellViewModel(records, server, clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.Queue.Total > 0 && shell.Navigation[2].Count.Length > 0);
        shell.GoTo(PageId.Work);
        var tabs = shell.Navigation.ToArray();
        var tab = shell.SelectedNav;
        var swaps = new List<string?>();
        shell.PropertyChanged += (_, e) => swaps.Add(e.PropertyName);
        var counts = new List<string?>();
        var observable = (object?)tab as INotifyPropertyChanged;
        if (observable is { } watched)
        {
            watched.PropertyChanged += (_, e) => counts.Add(e.PropertyName);
        }

        foreach (var view in _views)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            var before = shell.Navigation[2].Count;
            server.QueueView = RouteAnswer.SpecShaped("queue", view);
            await shell.Queue.Refresh(new Uri("http://127.0.0.1:7410/"), Token);   // awaited on the one thread: an event's refresh would race the reads
            Assert.NotEqual(before, shell.Navigation[2].Count, StringComparer.Ordinal);
            Assert.Same(tab, shell.SelectedNav);
        }

        Assert.Equal(tabs, shell.Navigation);
        Assert.DoesNotContain(swaps, name => name == nameof(ShellViewModel.Navigation));
        Assert.Contains(counts, name => name == "Count");
    }, Token);

    private static async Task Until(Func<bool> condition, string? what = null)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition(), "the condition never held " + what);
    }
}
