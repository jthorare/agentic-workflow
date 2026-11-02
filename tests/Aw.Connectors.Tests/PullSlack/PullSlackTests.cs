using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Connectors.Features.PullSlack;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.Slack;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Tests.PullSlack;

public sealed class PullSlackTests
{
    private const string Workspace = "example";
    private static SlackChannelRef General => new("C0001GEN", "general");
    private static SlackChannelRef Ops => new("C0002OPS", "ops");

    private static DateTimeOffset Yesterday => new(2026, 10, 26, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Today => new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string At(DateTimeOffset at) => SlackTs.Format(at);

    private static string Today_(int minutes) => At(Today.AddMinutes(minutes));

    private static FakeSlackReader Reader() => new(General.Channel, Ops.Channel);

    private static IService<PullSlackRequest, PullSlackResponse> Service(FakeSlackReader reader)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISlackReader>(reader).AddConnectorsFeatures();
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IService<PullSlackRequest, PullSlackResponse>>();
    }

    private static PullSlackRequest Request(SlackCursor? after = null, int limit = 100, params SlackChannelRef[] channels)
        => new(channels.Length == 0 ? [General] : channels, Workspace, after ?? SlackCursor.Empty, limit);

    [Fact, Trait("Lane", "unit")]
    public async Task A_thread_is_one_record_with_the_root_then_the_replies_in_order()
    {
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(10)).InThread(Today_(0)).By("Ada").WithText("Rerun fixed it"))
            .Add(General.Channel, A.SlackMessage(Today_(0)).By("Ada").WithText("Why is deploy red?").WithReplies(2))
            .Add(General.Channel, A.SlackMessage(Today_(5)).InThread(Today_(0)).By("Bo").WithText("Flaky test, see <https://ci.example/run/9|run 9>"));

        var response = (await Service(reader).Handle(Request(), Ct)).Value;

        var item = Assert.Single(response.Items);
        Assert.Equal($"slack:C0001GEN/{Today_(0)}", item.Key);
        Assert.Equal("message", item.Kind);
        Assert.Equal("#general: Why is deploy red?", item.Title);
        Assert.Equal(
            "#general: Why is deploy red?\n\nAda: Why is deploy red?\nBo: Flaky test, see run 9 (https://ci.example/run/9)\nAda: Rerun fixed it",
            item.Text);
        Assert.Equal("Ada", item.Author);
        Assert.Equal($"https://example.slack.com/archives/C0001GEN/p{Today_(0).Replace(".", string.Empty, StringComparison.Ordinal)}", item.Url);
        Assert.Equal("open", item.State);
        Assert.Equal(Today, item.CreatedAt);
        Assert.Equal(Today.AddMinutes(10), item.UpdatedAt);
        Assert.Null(item.ClosedAt);
        Assert.Equal("2026-10-27T09:10:00.0000000Z", item.Version);
        Assert.Empty(item.Links);
        Assert.Null(item.Repo);
        Assert.Contains("replies C0001GEN " + Today_(0), reader.Reads, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_replys_ts_never_makes_a_record_of_its_own()
    {
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(0)).WithText("root"))
            .Add(General.Channel, A.SlackMessage(Today_(1)).InThread(Today_(0)).WithText("reply"));

        var response = (await Service(reader).Handle(Request(), Ct)).Value;

        Assert.Equal([$"slack:C0001GEN/{Today_(0)}"], response.Items.Select(static i => i.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unthreaded_messages_are_one_record_per_channel_per_day()
    {
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(5)).By("Bo").WithText("two"))
            .Add(General.Channel, A.SlackMessage(Today_(0)).By("Ada").WithText("one"))
            .Add(General.Channel, A.SlackMessage(At(Yesterday)).By("Ada").WithText("yesterday"))
            .Add(Ops.Channel, A.SlackMessage(Today_(1)).By("Cy").WithText("ops only"));

        var response = (await Service(reader).Handle(Request(after: null, 100, General, Ops), Ct)).Value;

        Assert.Equal(["slack:C0001GEN/day/2026-10-26", "slack:C0001GEN/day/2026-10-27", "slack:C0002OPS/day/2026-10-27"], response.Items.Select(static i => i.Key), StringComparer.Ordinal);
        var today = response.Items[1];
        Assert.Equal("#general 2026-10-27", today.Title);
        Assert.Equal("#general 2026-10-27\n\nAda: one\nBo: two", today.Text);
        Assert.Null(today.Author);
        Assert.Equal(Today, today.CreatedAt);
        Assert.Equal(Today.AddMinutes(5), today.UpdatedAt);
        Assert.Equal("2026-10-27T09:05:00.0000000Z", today.Version);
        Assert.Equal($"https://example.slack.com/archives/C0001GEN/p{Today_(0).Replace(".", string.Empty, StringComparison.Ordinal)}", today.Url);
        Assert.Equal("message", today.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Join_messages_and_empty_messages_are_not_records_but_the_watermark_still_passes_them()
    {
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(0)).WithSubtype("channel_join").WithText("<@U1> has joined the channel"))
            .Add(General.Channel, A.SlackMessage(Today_(1)).WithText("  "));

        var response = (await Service(reader).Handle(Request(), Ct)).Value;

        Assert.Empty(response.Items);
        Assert.Equal(Today_(1), response.Next.Newest(General.Channel));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_cursor_holds_the_newest_root_ts_per_channel()
    {
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(0)))
            .Add(General.Channel, A.SlackMessage(Today_(7)))
            .Add(Ops.Channel, A.SlackMessage(Today_(3)));

        var response = (await Service(reader).Handle(Request(after: null, 100, General, Ops), Ct)).Value;

        Assert.Equal(Today_(7), response.Next.Newest(General.Channel));
        Assert.Equal(Today_(3), response.Next.Newest(Ops.Channel));
        Assert.Null(response.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_next_pull_starts_at_the_day_of_the_watermark_so_a_day_record_stays_whole()
    {
        var reader = Reader().Add(General.Channel, A.SlackMessage(Today_(0)).By("Ada").WithText("one"));
        var service = Service(reader);
        var first = (await service.Handle(Request(), Ct)).Value;
        _ = reader.Add(General.Channel, A.SlackMessage(Today_(30)).By("Bo").WithText("later"));

        var second = (await service.Handle(Request(first.Next), Ct)).Value;

        var item = Assert.Single(second.Items);
        Assert.Equal("slack:C0001GEN/day/2026-10-27", item.Key);
        Assert.Equal("#general 2026-10-27\n\nAda: one\nBo: later", item.Text);
        Assert.Equal($"history C0001GEN {At(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero))}", reader.Reads[^1]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_channel_with_no_watermark_starts_at_the_window_start_or_at_the_beginning()
    {
        var reader = Reader().Add(General.Channel, A.SlackMessage(Today_(0)));
        var windowStart = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        _ = await Service(reader).Handle(Request() with { WindowStart = windowStart }, Ct);
        _ = await Service(reader).Handle(Request(), Ct);

        Assert.Equal([$"history C0001GEN {At(windowStart)}", "history C0001GEN "], reader.Reads, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_on_a_later_channel_keeps_the_earlier_channels_records_and_leaves_that_channels_cursor_alone()
    {
        var reset = new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(0)).WithText("general news"))
            .Add(Ops.Channel, A.SlackMessage(Today_(1)).WithText("ops news"))
            .RateLimitHistory(reset, Ops.Channel);

        var response = (await Service(reader).Handle(Request(after: null, 100, General, Ops), Ct)).Value;

        Assert.Equal(reset, response.RateLimitedUntil);
        Assert.Equal(["slack:C0001GEN/day/2026-10-27"], response.Items.Select(static i => i.Key), StringComparer.Ordinal);
        Assert.Equal(Today_(0), response.Next.Newest(General.Channel));
        Assert.Null(response.Next.Newest(Ops.Channel));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_while_reading_a_thread_keeps_the_records_already_read_and_the_place_inside_the_channel()
    {
        var reset = new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(0)).WithText("calm"))
            .Add(Ops.Channel, A.SlackMessage(Today_(1)).WithText("unthreaded"))
            .Add(Ops.Channel, A.SlackMessage(Today_(2)).WithText("root"))
            .Add(Ops.Channel, A.SlackMessage(Today_(3)).InThread(Today_(2)).WithText("reply"))
            .RateLimitReplies(reset, Ops.Channel);

        var response = (await Service(reader).Handle(Request(after: null, 100, General, Ops), Ct)).Value;

        Assert.Equal(reset, response.RateLimitedUntil);
        Assert.Equal(["slack:C0001GEN/day/2026-10-27", "slack:C0002OPS/day/2026-10-27"], response.Items.Select(static i => i.Key), StringComparer.Ordinal);
        Assert.Null(response.Next.Newest(Ops.Channel));
        Assert.Equal(Today_(2), response.Next.Pass(Ops.Channel)!.Top);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_a_rate_limit_the_next_pull_resumes_from_the_cursor_and_reads_the_channel_that_was_cut()
    {
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(0)).WithText("general news"))
            .Add(Ops.Channel, A.SlackMessage(Today_(1)).WithText("ops news"))
            .RateLimitHistory(Today.AddHours(1), Ops.Channel);
        var service = Service(reader);
        var first = (await service.Handle(Request(after: null, 100, General, Ops), Ct)).Value;
        _ = reader.RateLimitHistory(until: null, Ops.Channel);

        var second = (await service.Handle(Request(first.Next, 100, General, Ops), Ct)).Value;

        Assert.Null(second.RateLimitedUntil);
        Assert.Contains("slack:C0002OPS/day/2026-10-27", second.Items.Select(static i => i.Key), StringComparer.Ordinal);
        Assert.Equal(Today_(1), second.Next.Newest(Ops.Channel));
        Assert.Equal(Today_(0), second.Next.Newest(General.Channel));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_limit_stops_the_run_before_the_next_channel_is_started()
    {
        var reader = Reader().Add(General.Channel, A.SlackMessage(Today_(0))).Add(Ops.Channel, A.SlackMessage(Today_(1)));

        var response = (await Service(reader).Handle(Request(after: null, 1, General, Ops), Ct)).Value;

        _ = Assert.Single(response.Items);
        Assert.Equal(["history C0001GEN "], reader.Reads, StringComparer.Ordinal);
        Assert.Null(response.Next.Newest(Ops.Channel));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_planted_slack_token_in_a_message_comes_out_redacted()
    {
        const string Planted = "xo" + "xb-1234567890-abcdefghijklmnop";
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(0)).By("Ada").WithText($"Here is the bot token {Planted} do not share").WithReplies(1))
            .Add(General.Channel, A.SlackMessage(Today_(1)).InThread(Today_(0)).By("Bo").WithText("and the app one xa" + "pp-1-A0123456789-abcdefghij"));

        var item = Assert.Single((await Service(reader).Handle(Request(), Ct)).Value.Items);

        Assert.DoesNotContain(Planted, item.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Planted, item.Title, StringComparison.Ordinal);
        Assert.DoesNotContain("xo" + "xb-", item.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("xa" + "pp-", item.Text, StringComparison.Ordinal);
        Assert.Contains("Here is the bot token [redacted] do not share", item.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_long_first_line_is_cut_in_the_title_and_a_message_without_an_author_reads_as_unknown()
    {
        var reader = Reader()
            .Add(General.Channel, A.SlackMessage(Today_(0)).By(author: null).WithText(new string('a', 100) + "\nsecond line").WithReplies(1))
            .Add(General.Channel, A.SlackMessage(Today_(1)).InThread(Today_(0)).By("Bo").WithText("ok"));

        var item = Assert.Single((await Service(reader).Handle(Request(), Ct)).Value.Items);

        Assert.Equal("#general: " + new string('a', 80) + "…", item.Title);
        Assert.Null(item.Author);
        Assert.Contains("\nunknown: " + new string('a', 100) + "\nsecond line", item.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_channel_the_reader_refuses_comes_back_as_its_error()
    {
        var reader = new FakeSlackReader(General.Channel).Add(Ops.Channel, A.SlackMessage(Today_(0)));

        var result = await Service(reader).Handle(Request(after: null, 100, General, Ops), Ct);

        Assert.Equal("SND-INDEX-013", result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reader_error_stops_the_run()
    {
        var reader = Reader().Fail(ConnectorsErrors.Unauthorized.With("nope"));

        Assert.Equal(ConnectorsErrors.Unauthorized.Code, (await Service(reader).Handle(Request(), Ct)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_audience_of_a_record_is_slack_and_its_channel_id() => Assert.Equal("slack:C0001GEN", SlackRecords.AudienceKey("C0001GEN"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("D0003DMX", 100)]
    [InlineData("C0001GEN", 0)]
    [InlineData("C0001GEN", 10001)]
    public async Task A_direct_message_id_or_a_bad_limit_is_refused_without_reading(string channel, int limit)
    {
        var reader = Reader();

        var result = await Service(reader).Handle(Request(after: null, limit, new SlackChannelRef(channel, "x")), Ct);

        Assert.Equal(ConnectorsErrors.Invalid.Code, result.Error.Code);
        Assert.Empty(reader.Reads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_channels_is_refused()
        => Assert.Equal(ConnectorsErrors.Invalid.Code, (await Service(Reader()).Handle(new PullSlackRequest([], Workspace, SlackCursor.Empty, 10), Ct)).Error.Code);
}
