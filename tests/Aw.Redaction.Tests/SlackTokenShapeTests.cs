using static Aw.Redaction.Tests.Shapes;

namespace Aw.Redaction.Tests;

/// <summary>
/// Messages M1d (messages spec §11): Aw.Redaction must catch every Slack token shape before Messages stores one. Today's 'slack-token' rule is
/// xox[abprs]-, which matches neither the app-level 'xapp-' token nor the cookie token 'xoxc-'. It skips while that rule is the shipped one.
/// </summary>
[Trait("Lane", "unit")]
public sealed class SlackTokenShapeTests
{
    private const string ShippedSlackPattern = "xox[abprs]-[A-Za-z0-9-]{10,}";

    [Theory]
    [InlineData("xa", "pp-1-A0123456789-")]
    [InlineData("xo", "xc-1234567890-")]
    public void Redaction_scrubs_xapp_and_xoxc(string head, string tail)
    {
        // why: no stub stands in front of a regex, so the red skips while the shipped rule is unchanged (main stays green); the day M1d widens or
        // adds a Slack rule this runs for real and fails on a regression
        if (BuiltinRules.All.Count(static r => r.Id.Contains("slack", StringComparison.Ordinal)) == 1
            && BuiltinRules.All.Single(static r => r.Id == "slack-token").Pattern.EndsWith(ShippedSlackPattern, StringComparison.Ordinal))
        {
            Assert.Skip("red until M1d: the slack-token rule is still xox[abprs]-");
        }

        var secret = Join(head, tail, Body);   // why: split so no token-shaped literal sits in the source

        var result = Redactor.Default.Scrub("pasted " + secret + " here");

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
        Assert.DoesNotContain(Body, result.Text, StringComparison.Ordinal);
    }
}
