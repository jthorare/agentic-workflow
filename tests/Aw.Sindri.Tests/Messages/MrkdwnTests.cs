using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1b (messages spec §6.3): Slack's mrkdwn parses and renders back to the same text, over the golden set of every span kind.</summary>
[Lane(TestLane.Unit)]
public sealed class MrkdwnTests
{
    [Theory]
    [InlineData("plain words")]
    [InlineData("*bold* and _italic_ and ~strike~")]
    [InlineData("run `dotnet build` first")]
    [InlineData("```\nvar x = 1;\n```")]
    [InlineData("> a quoted line")]
    [InlineData("see <https://github.com/acme/harbor/pull/3120|#3120>")]
    [InlineData("<@U0KIM> can you check?")]
    [InlineData("in <#C0HARBOR|harbor> today")]
    [InlineData("<!here> lunch Friday?")]
    [InlineData("shipped :tada:")]
    [InlineData("5 &lt; 6 &amp;&amp; 7 &gt; 2")]
    public void Mrkdwn_round_trips_the_golden_set(string text) =>
        RedUntil.Phase("M1b", () => Assert.Equal(text, Mrkdwn.Parse(text).Render()));
}
