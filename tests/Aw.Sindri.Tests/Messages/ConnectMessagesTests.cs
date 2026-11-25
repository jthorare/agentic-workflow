using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Features.ConnectMessages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1d (messages spec §5.1, §10): 'sindri messages connect' takes only a User OAuth Token; a bot, app-level or cookie token is refused by prefix before any call (SND-MSG-030).</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectMessagesTests
{
    [Theory]
    [InlineData("xo", "xb-")]
    [InlineData("xa", "pp-1-")]
    [InlineData("xo", "xc-")]
    public async Task Wrong_token_prefix_is_refused_before_any_call(string head, string tail)
    {
        // why: the prefix is split so no token-shaped literal sits in the source
        var token = head + tail + "0000000000-A0B1C2D3E4";

        var answer = await new ConnectMessagesService().Handle(new ConnectMessagesRequest(token, Manifest: false), TestContext.Current.CancellationToken);

        Assert.True(answer.IsError);
        Assert.Equal("SND-MSG-030", answer.Error.Code);
        Assert.DoesNotContain("A0B1C2D3E4", answer.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("xoxp")]
    public void Anything_but_a_user_token_is_refused(string? token)
    {
        var checkedToken = SlackUserToken.Check(token);

        Assert.True(checkedToken.IsError);
        Assert.Equal("SND-MSG-030", checkedToken.Error.Code);
    }

    [Fact]
    public void A_user_token_passes_the_prefix_check()
    {
        var token = string.Concat("xo", "xp-", "1234567890-A0B1C2D3E4");   // why: split so no token-shaped literal sits in the source

        Assert.Equal(token, SlackUserToken.Check(token).Value);
    }
}
