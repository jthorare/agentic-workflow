using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>A served reason gets a capital only when its first word is plain letters; a key, an id or a number leaves the text as the server sent it (#2550).</summary>
[Lane(TestLane.Unit)]
public sealed class CapitalisedTests
{
    [Theory]
    [InlineData("no run 'x'", "No run 'x'")]
    [InlineData("nope", "Nope")]
    [InlineData("refused", "Refused")]
    [InlineData("review.login is the owner's to set", "review.login is the owner's to set")]
    [InlineData("workflow:triage has a run", "workflow:triage has a run")]
    [InlineData("gh_token missing", "gh_token missing")]
    [InlineData("gh-2550 is closed", "gh-2550 is closed")]
    [InlineData("'docs' is kept", "'docs' is kept")]
    [InlineData("v2 settings moved", "v2 settings moved")]
    [InlineData("2 runs left", "2 runs left")]
    [InlineData("Already", "Already")]
    [InlineData("", "")]
    public void Only_a_plain_first_word_gets_a_capital(string text, string expected) =>
        Assert.Equal(expected, Labels.Capitalised(text));
}
