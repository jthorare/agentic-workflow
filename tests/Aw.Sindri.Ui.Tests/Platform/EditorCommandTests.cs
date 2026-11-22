using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Tests.Platform;

/// <summary>The editor command a code file opens in: the configured one, then VISUAL, then EDITOR, as an argv list (#3715).</summary>
[Lane(TestLane.Unit)]
public sealed class EditorCommandTests
{
    [Fact]
    public void The_configured_command_wins_over_VISUAL_and_EDITOR()
    {
        var argv = EditorCommand.Resolve(new EditorEnvironment("zed --wait", "code", "vim"));

        Assert.Equal(["zed", "--wait"], argv, StringComparer.Ordinal);
    }

    [Fact]
    public void VISUAL_wins_over_EDITOR()
    {
        Assert.Equal(["code"], EditorCommand.Resolve(new EditorEnvironment(null, "code", "vim")), StringComparer.Ordinal);
    }

    [Fact]
    public void EDITOR_is_used_when_nothing_else_is_set()
    {
        Assert.Equal(["nano"], EditorCommand.Resolve(new EditorEnvironment(null, null, "nano")), StringComparer.Ordinal);
    }

    [Fact]
    public void A_blank_value_is_skipped_to_the_next_one()
    {
        Assert.Equal(["vim"], EditorCommand.Resolve(new EditorEnvironment("  ", string.Empty, "vim")), StringComparer.Ordinal);
    }

    [Fact]
    public void Nothing_set_is_null_so_the_system_default_opens_the_file()
    {
        Assert.Null(EditorCommand.Resolve(new EditorEnvironment(null, " ", null)));
    }

    [Fact]
    public void The_value_splits_on_any_run_of_whitespace_and_shell_characters_stay_literal()
    {
        var argv = EditorCommand.Resolve(new EditorEnvironment("code   --wait\t$HOME;rm", null, null));

        Assert.Equal(["code", "--wait", "$HOME;rm"], argv, StringComparer.Ordinal);
    }
}
