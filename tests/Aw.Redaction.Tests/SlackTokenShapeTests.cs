using static Aw.Redaction.Tests.Shapes;

namespace Aw.Redaction.Tests;

/// <summary>
/// Messages M1d (messages spec §11): Aw.Redaction must catch every Slack token shape before Messages stores one. Today's 'slack-token' rule is
/// xox[abprs]-, which matches neither the app-level 'xapp-' token nor the cookie token 'xoxc-'.
/// </summary>
[Trait("Lane", "unit")]
public sealed class SlackTokenShapeTests
{
    [Theory]
    [InlineData("xa", "pp-1-A0123456789-")]
    [InlineData("xo", "xc-1234567890-")]
    public void Redaction_scrubs_xapp_and_xoxc(string head, string tail)
    {
        var secret = Join(head, tail, Body);   // why: split so no token-shaped literal sits in the source

        var result = Redactor.Default.Scrub("pasted " + secret + " here");

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
        Assert.DoesNotContain(Body, result.Text, StringComparison.Ordinal);
    }
}
