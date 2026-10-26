using System.Globalization;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Testing.History;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.QueryContext;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.GetHistory;

/// <summary>A history read that takes longer than the default context deadline on a big repo must still answer (#1221, owner rule q93: no arbitrary timeouts).</summary>
public sealed class HistoryDeadlineTests : IDisposable
{
    private const string Anchor = "feature:layout.split-pane";
    private readonly List<IDisposable> _worlds = [];

    public void Dispose()
    {
        foreach (var world in _worlds)
        {
            world.Dispose();
        }
    }

    // One git read that takes 15 s of fake clock to finish (the default context.deadline is 10 s) and then lists its changes.
    [Fact, Trait("Lane", "unit")]
    public async Task A_read_that_outlasts_the_default_deadline_still_lists_its_changes_and_is_not_degraded()
    {
        var landed = new FileChange(new string('3', 40), DateTimeOffset.Parse("2026-07-20T10:00:00Z", CultureInfo.InvariantCulture), "Tidy the split (#6)", Parents: 1, ["src/layout/SplitPane.ts"]);
        var inner = new FakeFileHistory(MapWorld.Repos[0], new HistoryWorld(new string('f', 40), [landed]));
        FakeTimeProvider? clock = null;
        var history = new ScriptedHistory(inner, async (repo, query, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(15), clock!, ct);
            return await inner.Changes(repo, query, ct);
        });
        using var world = new MapWorld(history: history);
        clock = world.Clock;
        _ = await world.ImportFixture();

        var view = (await Engines.Advancing(world.Clock, () => world.Engine.History([Anchor], HistoryWindow.Of(since: null, before: null).Value, MapRead.Current, MapWorld.Human, MapWorld.None).AsTask())).Value;

        var row = view.Searched.Sources.First(s => s.Id.Value == "git-history");
        Assert.NotEqual(SearchedState.Degraded, row.State);
        Assert.Empty(view.Degraded);
        Assert.NotEmpty(view.Changes);
    }
}
