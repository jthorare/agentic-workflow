using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Tests.Platform;

/// <summary>The editor command a code file opens in: the configured one, then VISUAL, then EDITOR, as an argv list (#3715); a terminal editor is told apart by its program name (#3836).</summary>
[Lane(TestLane.Unit)]
public sealed class EditorCommandTests
{
    [Fact]
    public void The_configured_command_wins_over_VISUAL_and_EDITOR()
    {
        var argv = EditorCommand.Resolve(new EditorEnvironment(Configured: "zed --wait", Visual: "code", Editor: "vim"));

        Assert.Equal(["zed", "--wait"], argv, StringComparer.Ordinal);
    }

    [Fact]
    public void VISUAL_wins_over_EDITOR() =>
        Assert.Equal(["code"], EditorCommand.Resolve(new EditorEnvironment(Configured: null, Visual: "code", Editor: "vim")), StringComparer.Ordinal);

    [Fact]
    public void EDITOR_is_used_when_nothing_else_is_set() =>
        Assert.Equal(["nano"], EditorCommand.Resolve(new EditorEnvironment(Configured: null, Visual: null, Editor: "nano")), StringComparer.Ordinal);

    [Fact]
    public void A_blank_value_is_skipped_to_the_next_one() =>
        Assert.Equal(["vim"], EditorCommand.Resolve(new EditorEnvironment(Configured: "  ", Visual: string.Empty, Editor: "vim")), StringComparer.Ordinal);

    [Fact]
    public void Nothing_set_is_null_so_the_system_default_opens_the_file() =>
        Assert.Null(EditorCommand.Resolve(new EditorEnvironment(Configured: null, Visual: " ", Editor: null)));

    [Fact]
    public void The_value_splits_on_any_run_of_whitespace_and_shell_characters_stay_literal()
    {
        var argv = EditorCommand.Resolve(new EditorEnvironment(Configured: "code   --wait\t$HOME;rm", Visual: null, Editor: null));

        Assert.Equal(["code", "--wait", "$HOME;rm"], argv, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("vim", true)]
    [InlineData("/usr/local/bin/nvim", true)]
    [InlineData("hx", true)]
    [InlineData("emacs -nw", true)]
    [InlineData("emacsclient -t", true)]
    [InlineData("emacs", false)]
    [InlineData("code -w", false)]
    [InlineData("gvim -f", false)]
    public void A_terminal_editor_is_known_by_its_program_name(string command, bool terminal) =>
        Assert.Equal(terminal, EditorCommand.IsTerminal(command.Split(' ')));
}
