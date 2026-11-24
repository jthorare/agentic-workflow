using static Aw.Redaction.Tests.Shapes;

namespace Aw.Redaction.Tests;

/// <summary>
/// Messages M1d (messages spec §11): Aw.Redaction catches every Slack token shape before Messages stores one: the app-level 'xapp-' token and the cookie
/// token 'xoxc-' too, which the old 'xox[abprs]-' rule missed.
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

    [Fact]
    public void Redaction_scrubs_the_rotating_form_whole()
    {
        var secret = Join("xo", "xe.xo", "xp-1-", Body);   // why: split so no token-shaped literal sits in the source

        var result = Redactor.Default.Scrub("pasted " + secret + " here");

        Assert.Equal(ScrubOutcome.Redacted, result.Outcome);
        Assert.DoesNotContain("xoxe", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Body, result.Text, StringComparison.Ordinal);
    }
}
