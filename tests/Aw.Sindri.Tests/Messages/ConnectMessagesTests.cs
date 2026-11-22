using Aw.Kernel.Testing;
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
    public Task Wrong_token_prefix_is_refused_before_any_call(string head, string tail) => RedUntil.Phase("M1d", async () =>
    {
        // why: the prefix is split so no token-shaped literal sits in the source
        var token = head + tail + "0000000000-A0B1C2D3E4";

        var answer = await new ConnectMessagesService().Handle(new ConnectMessagesRequest(token, Manifest: false), TestContext.Current.CancellationToken);

        Assert.True(answer.IsError);
        Assert.Equal("SND-MSG-030", answer.Error.Code);
    });
}
