using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch.Argv;

namespace Aw.Sindri.Tests.Launch.Argv;

/// <summary>The closed placeholder set (launcher spec section 4): one argument each, no shell, no option smuggling.</summary>
public sealed class PlaceholdersTests
{
    private static PlaceholderValues Values(string? title = null, string? seed = null, string? cwd = null, string? sessionId = null, IReadOnlyList<string>? cmd = null) =>
        new("claude", cwd ?? "/repo", title ?? "sindri ITM-1", "sindri ITM-1", seed ?? "seed", sessionId is null ? null : SessionId.From(sessionId), McpConfig: null, cmd);

    [Theory, Trait("Lane", "unit")]
    [InlineData("two words")]
    [InlineData("it's \"quoted\"")]
    [InlineData("cost $HOME `x` $(y)")]
    [InlineData("line one\nline two")]
    public void Each_placeholder_expands_to_one_argument(string seed)
    {
        var argv = Placeholders.Expand(["{bin}", "--message", "{seed}"], Values(seed: seed));

        Assert.Equal(["claude", "--message", seed], argv.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Cmd_splices_attach_argv_as_separate_arguments()
    {
        var argv = Placeholders.Expand(["kitty", "--title", "{title}", "--directory", "{cwd}", "--", "{cmd...}"], Values(cmd: ["tmux", "-L", "sindri", "attach", "-t", "=snd-LCH-1"]));

        Assert.Equal(["kitty", "--title", "sindri ITM-1", "--directory", "/repo", "--", "tmux", "-L", "sindri", "attach", "-t", "=snd-LCH-1"], argv.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Cmd_without_an_attach_argv_is_SND_LAUNCH_011()
    {
        var argv = Placeholders.Expand(["{cmd...}"], Values());

        Assert.Equal("SND-LAUNCH-011", argv.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{title}", "-x")]
    [InlineData("{name}", "--version")]
    [InlineData("{seed}", "-p")]
    [InlineData("{title}-suffix", "-x")]
    public void Expanded_title_name_sessionId_or_seed_starting_with_dash_is_refused(string element, string value)
    {
        var values = Values(title: value, seed: value, sessionId: value) with { Name = value };

        var argv = Placeholders.Expand(["{bin}", element], values);

        Assert.Equal("SND-REQ-001", argv.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dash_value_inside_an_argument_that_does_not_start_with_it_is_not_an_option()
    {
        var argv = Placeholders.Expand(["{bin}", "--title={title}"], Values(title: "-x"));

        Assert.Equal(["claude", "--title=-x"], argv.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("relative/repo")]
    [InlineData("")]
    [InlineData("~/repo")]
    public void Cwd_must_be_absolute(string cwd)
    {
        var argv = Placeholders.Expand(["{bin}", "-C", "{cwd}"], Values(cwd: cwd));

        Assert.Equal("SND-REQ-001", argv.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Embedded_placeholder_expands_inside_its_argument()
    {
        var argv = Placeholders.Expand(["{bin}", "--resume={sessionId}"], Values(sessionId: "0b1f"));

        Assert.Equal(["claude", "--resume=0b1f"], argv.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_value_that_looks_like_a_placeholder_is_not_expanded_again()
    {
        var argv = Placeholders.Expand(["{bin}", "{seed}"], Values(seed: "{sessionId} {cmd...}"));

        Assert.Equal(["claude", "{sessionId} {cmd...}"], argv.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_placeholder_with_no_value_is_SND_LAUNCH_011()
    {
        var argv = Placeholders.Expand(["{bin}", "--resume={sessionId}"], Values());

        Assert.Equal("SND-LAUNCH-011", argv.Error.Code);
    }
}
