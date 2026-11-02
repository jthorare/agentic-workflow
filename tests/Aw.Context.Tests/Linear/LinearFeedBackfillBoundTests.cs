using System.Globalization;
using Aw.Context.Core.Sync;

namespace Aw.Context.Tests.Linear;

/// <summary>A Backfill pull is bounded on both sides (#1512): the issues updated from the window's start up to, not including, where Recent started.</summary>
[Trait("Lane", "unit")]
public sealed class LinearFeedBackfillBoundTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_pull_with_a_Before_sends_updatedAt_gte_the_window_start_and_lt_Before()
    {
        var (feed, fake) = LinearFeedContractTests.FeedOf([FakeLinearIssues.Node("n", _at)]);
        var windowStart = _at.AddDays(-183);
        var before = _at.AddDays(-30);

        _ = await feed.Pull(new RecordPull(After: null, 10, windowStart, before), TestContext.Current.CancellationToken);

        var updatedAt = fake.Variables[0]["filter"]?["updatedAt"];
        Assert.Equal(
            (windowStart, before),
            (Parse(updatedAt?["gte"]?.GetValue<string>()), Parse(updatedAt?["lt"]?.GetValue<string>())));
    }

    private static DateTimeOffset? Parse(string? iso) => iso is null ? null : DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);
}
