using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Kernel;

namespace Aw.Connectors.Tests.Slack;

public sealed class SlackSettingsTests
{
    private const string Pointer = "env:SLACK_READ_TOKEN";

    private static SlackChannelRef General => new("C0001GEN", "general");

    private static Result<SlackSettings> Make(IReadOnlyList<SlackChannelRef> channels, string? token = Pointer, string workspace = "example")
        => SlackSettings.Create(channels, token, workspace);

    [Fact, Trait("Lane", "unit")]
    public void Valid_values_make_settings_and_a_repeated_channel_id_counts_once()
    {
        var settings = Make([General, new SlackChannelRef("G0002OPS", "ops"), new SlackChannelRef("C0001GEN", "renamed")]).Value;

        Assert.Equal(["C0001GEN", "G0002OPS"], settings.Channels.Select(static c => c.Channel), StringComparer.Ordinal);
        Assert.Equal("general", settings.Channels[0].Name);
        Assert.Equal(Pointer, settings.Token);
        Assert.Equal("example", settings.Workspace);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_channels_is_refused() => Assert.Equal(ConnectorsErrors.Invalid.Code, Make([]).Error.Code);

    [Theory, Trait("Lane", "unit")]
    [InlineData("D0003DMX")]
    [InlineData("c0001gen")]
    [InlineData("general")]
    [InlineData("C")]
    [InlineData("")]
    public void A_direct_message_or_anything_that_is_not_a_channel_id_is_refused(string id)
        => Assert.Equal(ConnectorsErrors.Invalid.Code, Make([General, new SlackChannelRef(id, "x")]).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_channel_without_a_name_is_refused() => Assert.True(Make([new SlackChannelRef("C0001GEN", " ")]).IsError);

    [Fact, Trait("Lane", "unit")]
    public void A_value_that_is_not_a_pointer_is_refused_and_never_repeated()
    {
        const string Planted = "xoxb-planted-value-0123456789";

        var error = Make([General], token: Planted).Error;

        Assert.Equal(ConnectorsErrors.Invalid.Code, error.Code);
        Assert.DoesNotContain(Planted, error.Message, StringComparison.Ordinal);
        Assert.Contains("context.sources.slack.token", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_token_yet_is_allowed_before_connect() => Assert.Null(Make([General], token: null).Value.Token);

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("Example")]
    [InlineData("a.b")]
    [InlineData("evil.com/x")]
    [InlineData("-lead")]
    public void A_workspace_that_is_not_a_slack_subdomain_name_is_refused(string workspace)
        => Assert.Equal(ConnectorsErrors.Invalid.Code, Make([General], workspace: workspace).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void ToString_withholds_the_pointer() => Assert.DoesNotContain(Pointer, Make([General]).Value.ToString(), StringComparison.Ordinal);
}
