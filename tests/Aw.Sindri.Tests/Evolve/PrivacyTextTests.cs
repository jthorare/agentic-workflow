using Aw.Kernel.Testing;
using Aw.Sindri.Core.Evolve;

namespace Aw.Sindri.Tests.Evolve;

/// <summary>The normalization every leak layer reads through, and the name splitting the terms and identifiers layers share.</summary>
[Lane(TestLane.Unit)]
public sealed class PrivacyTextTests
{
    [Fact]
    public void Visible_removes_zero_width_folds_fullwidth_and_white_space() =>
        Assert.Equal("Acme Care", PrivacyText.Visible("\uFF21cme\u200B\u00A0\n Care "));

    [Fact]
    public void Visible_composes_decomposed_accents() => Assert.Equal("caf\u00E9", PrivacyText.Visible("cafe\u0301"));

    [Fact]
    public void Words_lowercase_and_split_on_everything_but_letters_and_digits() =>
        Assert.Equal(["run", "it", "in", "ci", "not", "locally"], PrivacyText.Words("Run it in CI, not\u2014locally!"));

    [Theory]
    [InlineData("AcmeCare", new[] { "acme", "care" })]
    [InlineData("GLXCare", new[] { "glx", "care" })]
    [InlineData("IBMWatson", new[] { "ibm", "watson" })]
    [InlineData("acme2", new[] { "acme", "2" })]
    [InlineData("useAcme", new[] { "use", "acme" })]
    [InlineData("ACMEClient", new[] { "acme", "client" })]
    [InlineData("Acmeville", new[] { "acmeville" })]
    [InlineData("GLX", new[] { "glx" })]
    public void Split_breaks_names_at_case_and_digit_boundaries(string word, string[] parts) => Assert.Equal(parts, NameParts.Split(word));
}
