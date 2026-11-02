using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The text a person typing in a sandboxed pane sees is the golden, and the status line fits (sandbox spec §7).</summary>
public sealed class SandboxStatusLineTests
{
    private static string Golden(string name) => SandboxGoldens.Text(name).TrimEnd('\n');

    [Fact, Trait("Lane", "unit")]
    public void The_step_status_line_is_the_golden() => Assert.Equal(Golden("statusline.txt"), SandboxStatusLine.Template(attended: false));

    [Fact, Trait("Lane", "unit")]
    public void The_attended_status_line_is_the_golden() => Assert.Equal(Golden("statusline-attended.txt"), SandboxStatusLine.Template(attended: true));

    [Fact, Trait("Lane", "unit")]
    public void The_takeover_banner_is_the_golden() => Assert.Equal(Golden("takeover-banner.txt"), SandboxStatusLine.TakeoverBanner);

    [Fact, Trait("Lane", "unit")]
    public void The_refusal_is_the_golden() => Assert.Equal(Golden("refusal.txt"), SandboxStatusLine.Refusal);

    [Theory, Trait("Lane", "unit")]
    [InlineData("C-b", "Ctrl-b")]
    [InlineData("C-a", "Ctrl-a")]
    [InlineData("M-a", "M-a")]
    [InlineData("`", "`")]
    [InlineData("C-Space", "C-Space")]
    public void The_prefix_is_shown_the_way_a_person_says_it(string option, string shown) => Assert.Equal(shown, SandboxStatusLine.PrefixLabel(option));

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, "C-b", "Sandboxed: push and secrets blocked. Outside it: Ctrl-b c")]
    [InlineData(true, "C-a", "Sandboxed (attended): push and secrets blocked. Outside it: Ctrl-a c")]
    public void The_line_names_the_prefix_of_the_session(bool attended, string option, string expected) => Assert.Equal(expected, SandboxStatusLine.Render(attended, option));

    [Theory, Trait("Lane", "unit")]
    [InlineData(false)]
    [InlineData(true)]
    public void The_line_stays_under_80_characters_even_with_a_long_prefix(bool attended)
    {
        Assert.True(SandboxStatusLine.Render(attended, "C-b").Length < 80);
        Assert.True(SandboxStatusLine.Render(attended, "C-Space").Length < 80);
        Assert.DoesNotContain(SandboxStatusLine.PrefixToken, SandboxStatusLine.Render(attended, "C-b"), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_refusal_has_no_rule_id() => Assert.DoesNotContain("SND-", SandboxStatusLine.Refusal, StringComparison.Ordinal);
}
