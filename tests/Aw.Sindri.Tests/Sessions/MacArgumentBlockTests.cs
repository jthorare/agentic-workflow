using System.Text;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>A process's argument block gives argv[0] and the allowlisted environment, and nothing else of it (collector spec §4.1 step 5, macOS).</summary>
public sealed class MacArgumentBlockTests
{
    private static byte[] Block(int argc, string path, string[] arguments, string[] environment, int padding = 3)
    {
        var bytes = new List<byte>(BitConverter.GetBytes(argc));
        bytes.AddRange(Encoding.UTF8.GetBytes(path));
        bytes.AddRange(new byte[1 + padding]);
        foreach (var text in arguments.Concat(environment))
        {
            bytes.AddRange(Encoding.UTF8.GetBytes(text));
            bytes.Add(0);
        }

        return [.. bytes];
    }

    [Fact, Trait("Lane", "unit")]
    public void The_block_gives_argv0_and_only_the_allowlisted_environment()
    {
        var block = Block(1, "/opt/claude/versions/2.1.9/claude", ["claude"], ["PATH=/usr/bin", "TMUX_PANE=%3", $"ANTHROPIC_API_KEY={ProcessTableContract.PlantedToken}", "TERM_PROGRAM=WarpTerminal"]);

        var arguments = MacArgumentBlock.Parse(block)!;

        Assert.Equal("claude", arguments.Argv0);
        Assert.Equal(["TERM_PROGRAM=WarpTerminal", "TMUX_PANE=%3"], arguments.Environment.Select(static p => $"{p.Key}={p.Value}").Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_other_arguments_are_skipped_by_count_so_a_name_in_a_prompt_is_not_an_environment_entry()
    {
        var block = Block(3, "/bin/claude", ["claude", "-p", "run with TMUX=/tmp/evil,1,0 set"], ["TMUX=/tmp/tmux-501/default,12,0"]);

        var arguments = MacArgumentBlock.Parse(block)!;

        Assert.Equal("/tmp/tmux-501/default,12,0", arguments.Environment["TMUX"]);
        _ = Assert.Single(arguments.Environment);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_argument_is_counted_and_a_value_may_hold_an_equals_sign()
    {
        var block = Block(3, "/bin/x", ["x", string.Empty, "z"], ["WARP_FOCUS_URL=warp://a?b=c"]);

        var arguments = MacArgumentBlock.Parse(block)!;

        Assert.Equal("x", arguments.Argv0);
        Assert.Equal("warp://a?b=c", arguments.Environment["WARP_FOCUS_URL"]);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(3)]
    public void A_block_too_short_to_hold_an_argument_count_and_path_is_not_a_block(int length) =>
        Assert.Null(MacArgumentBlock.Parse(new byte[length]));

    [Fact, Trait("Lane", "unit")]
    public void A_block_with_no_environment_gives_an_empty_one()
    {
        var arguments = MacArgumentBlock.Parse(Block(1, "/bin/x", ["x"], []))!;

        Assert.Equal(("x", 0), (arguments.Argv0, arguments.Environment.Count));
    }
}
