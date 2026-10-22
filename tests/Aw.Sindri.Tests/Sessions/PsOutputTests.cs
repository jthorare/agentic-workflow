using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>ps's rows become the current user's live processes (collector spec §4.1 step 1, macOS).</summary>
public sealed class PsOutputTests
{
    private static IReadOnlyList<PsRow> Rows(int uid = 501) => PsOutput.Parse(File.ReadAllText(Path.Combine(SessionFixtures.Macos, "ps.txt")), uid);

    [Fact, Trait("Lane", "unit")]
    public void Rows_hold_the_users_live_processes_and_skip_zombies_and_other_users()
    {
        var pids = Rows().Select(static r => r.Pid).ToList();

        Assert.DoesNotContain(1, pids);     // root's
        Assert.DoesNotContain(669, pids);   // root's, with a space in its name
        Assert.DoesNotContain(759, pids);   // a zombie
        Assert.Contains(8184, pids);
        Assert.Equal([1, 669], Rows(uid: 0).Select(static r => r.Pid));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_row_gives_its_parent_its_command_with_spaces_and_its_start_as_epoch_seconds()
    {
        var claude = Rows().Single(static r => r.Pid == 8184);
        var npm = Rows().Single(static r => r.Pid == 10075);

        Assert.Equal((7869, "claude", new DateTimeOffset(2026, 10, 21, 15, 15, 58, TimeSpan.Zero).ToUnixTimeSeconds()), (claude.ParentPid, claude.Command, claude.StartTicks));
        Assert.Equal("npm exec xcodebuildmcp@2.3.0 mcp", npm.Command);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_terminal_is_a_nonzero_tty_and_no_terminal_is_zero()
    {
        Assert.NotEqual(0, Rows().Single(static r => r.Pid == 8184).Tty);   // ttys000
        Assert.Equal(0, Rows().Single(static r => r.Pid == 5390).Tty);      // ??
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not a ps line at all")]
    [InlineData("  12  1  501 S ?? Wed Oct 21 15:12:13 2026")]
    [InlineData("  12  1  501 S ?? Fri Foo 21 15:12:13 2026 /bin/x")]
    public void Lines_that_are_not_rows_are_skipped(string line) => Assert.Empty(PsOutput.Parse(line, 501));
}
