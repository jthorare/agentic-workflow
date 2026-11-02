using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.Slack;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Tests.PullSlack;

public sealed class SlackConnectorTests
{
    private static SlackChannelRef General => new("C0001GEN", "general");
    private static SlackChannelRef Ops => new("G0002OPS", "ops");

    private static DateTimeOffset Today => new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string At(int minutes) => SlackTs.Format(Today.AddMinutes(minutes));

    private static IConnector Connector(FakeSlackReader reader)
    {
        var settings = SlackSettings.Create([General, Ops], "env:SLACK_READ_TOKEN", "example").Value;
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISlackReader>(reader).AddConnectorsFeatures().AddSlackConnector(settings);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IConnector>();
    }

    private static FakeSlackReader Reader() => new(General.Channel, Ops.Channel);

    [Fact, Trait("Lane", "unit")]
    public void It_describes_itself_as_slack()
    {
        var description = Connector(Reader()).Describe();

        Assert.Equal("slack", description.Source);
        Assert.Equal("Slack", description.Name);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_pull_reads_every_listed_channel_and_hands_back_a_cursor_as_text()
    {
        var reader = Reader().Add(General.Channel, A.SlackMessage(At(0))).Add(Ops.Channel, A.SlackMessage(At(4)));

        var page = (await Connector(reader).Pull(after: null, 100, before: null, Ct)).Value;

        Assert.Equal(["slack:C0001GEN/day/2026-10-27", "slack:G0002OPS/day/2026-10-27"], page.Items.Select(static i => i.Key), StringComparer.Ordinal);
        Assert.Equal($"C0001GEN={At(0)},G0002OPS={At(4)}", page.Next.Value);
        Assert.Empty(page.Removed);
        Assert.False(page.CursorReset);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_resumes_from_the_cursor_it_handed_back()
    {
        var reader = Reader().Add(General.Channel, A.SlackMessage(At(0)));
        var connector = Connector(reader);
        var first = (await connector.Pull(after: null, 100, before: null, Ct)).Value;
        _ = reader.Add(General.Channel, A.SlackMessage(At(40)).WithText("later"));

        var second = (await connector.Pull(first.Next, 100, before: null, Ct)).Value;

        Assert.Contains("later", Assert.Single(second.Items).Text, StringComparison.Ordinal);
        Assert.Equal($"C0001GEN={At(40)}", second.Next.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cursor_it_cannot_read_starts_over_and_says_so()
    {
        var reader = Reader().Add(General.Channel, A.SlackMessage(At(0)));

        var page = (await Connector(reader).Pull(new SourceCursor("garbage"), 100, before: null, Ct)).Value;

        Assert.True(page.CursorReset);
        _ = Assert.Single(page.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_that_stops_a_pull_before_any_record_is_an_error_with_the_reset_time()
    {
        var reset = new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
        var reader = Reader().Add(General.Channel, A.SlackMessage(At(0))).RateLimitHistory(reset);

        var error = (await Connector(reader).Pull(after: null, 100, before: null, Ct)).Error;

        Assert.Equal(ConnectorsErrors.RateLimited.Code, error.Code);
        Assert.Contains("2026-10-27T10:00:00", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_after_some_progress_returns_the_progress_and_the_next_pull_reports_the_limit()
    {
        var reset = new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
        var reader = Reader().Add(General.Channel, A.SlackMessage(At(0))).Add(Ops.Channel, A.SlackMessage(At(4))).RateLimitHistory(reset, Ops.Channel);
        var connector = Connector(reader);

        var page = (await connector.Pull(after: null, 100, before: null, Ct)).Value;

        _ = Assert.Single(page.Items);
        Assert.Equal($"C0001GEN={At(0)}", page.Next.Value);
        Assert.Equal(ConnectorsErrors.RateLimited.Code, (await connector.Pull(page.Next, 100, before: null, Ct)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_threads_version_is_its_newest_reply_and_a_days_version_its_newest_message()
    {
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(At(0)))
            .Add(General.Channel, A.SlackMessage(At(5)).InThread(At(0)))
            .Add(Ops.Channel, A.SlackMessage(At(10)))
            .Add(Ops.Channel, A.SlackMessage(At(12)));

        var versions = (await Connector(reader).Versions([$"slack:C0001GEN/{At(0)}", "slack:G0002OPS/day/2026-10-27"], Ct)).Value;

        Assert.Equal("2026-10-27T09:05:00.0000000Z", versions[$"slack:C0001GEN/{At(0)}"]);
        Assert.Equal("2026-10-27T09:12:00.0000000Z", versions["slack:G0002OPS/day/2026-10-27"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_key_the_source_no_longer_has_or_that_is_not_slacks_is_absent()
    {
        var reader = Reader().Add(General.Channel, A.SlackMessage(At(0)));

        var versions = (await Connector(reader).Versions(["slack:C0001GEN/1700000000.000100", "slack:C0001GEN/day/2020-01-01", "github:issue/acme/widgets/1", "slack:C0999OTH/1700000000.000100"], Ct)).Value;

        Assert.Empty(versions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_probe_checks_the_first_listed_channel_with_one_cheap_read()
    {
        var reader = Reader();

        var result = await Connector(reader).Probe(Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(["probe C0001GEN"], reader.Reads, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_probe_that_meets_the_rate_limit_says_so()
    {
        var reader = Reader().RateLimitHistory(Today.AddHours(1));

        Assert.Equal(ConnectorsErrors.RateLimited.Code, (await Connector(reader).Probe(Ct)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_probe_error_comes_back_as_it_is()
    {
        var reader = Reader().Fail(ConnectorsErrors.Unauthorized.With("nope"));

        Assert.Equal(ConnectorsErrors.Unauthorized.Code, (await Connector(reader).Probe(Ct)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_that_finds_nothing_hands_back_a_cursor_that_is_not_empty_and_reads_as_a_first_run()
    {
        var reader = Reader().Add(General.Channel, A.SlackMessage(At(0)));
        var before = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        var page = (await Connector(reader).Pull(after: null, 100, before, Ct)).Value;

        Assert.Empty(page.Items);
        Assert.Equal("-", page.Next.Value);
        Assert.Same(SlackCursor.Empty, SlackCursor.Parse(page.Next.Value));
    }
}
