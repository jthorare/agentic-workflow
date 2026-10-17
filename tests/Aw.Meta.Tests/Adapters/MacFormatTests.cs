using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Tests.Adapters;

public sealed class MacFormatTests
{
    private const string VmStat =
        "Mach Virtual Memory Statistics: (page size of 16384 bytes)\n"
        + "Pages free:                                   101677.\n"
        + "Pages active:                                1289976.\n"
        + "Pages inactive:                              1269176.\n"
        + "Pages speculative:                             20193.\n"
        + "Pages throttled:                                   0.\n";

    [Fact, Trait("Lane", "unit")]
    public void Total_memory_is_the_memsize_in_kilobytes() => Assert.Equal(50_331_648L, MacFormat.MemTotalKb("51539607552\n"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not a number")]
    public void A_memsize_that_is_not_a_number_is_a_format_error(string text) => Assert.Throws<FormatException>(() => MacFormat.MemTotalKb(text));

    [Fact, Trait("Lane", "unit")]
    public void Available_memory_is_free_inactive_and_speculative_pages() =>
        Assert.Equal(1_391_046L * 16, MacFormat.AvailableKb(VmStat));

    [Fact, Trait("Lane", "unit")]
    public void Available_memory_uses_the_reported_page_size() =>
        Assert.Equal(240L, MacFormat.AvailableKb("Mach Virtual Memory Statistics: (page size of 4096 bytes)\nPages free: 10.\nPages inactive: 20.\nPages speculative: 30.\n"));

    [Fact, Trait("Lane", "unit")]
    public void A_vm_stat_with_no_page_size_is_a_format_error() => Assert.Throws<FormatException>(() => MacFormat.AvailableKb("Pages free: 10.\n"));

    [Fact, Trait("Lane", "unit")]
    public void Swap_usage_gives_total_and_used_in_kilobytes()
    {
        var (total, used) = MacFormat.SwapUsage("total = 5632.00M  used = 4025.50M  free = 1606.50M  (encrypted)\n");

        Assert.Equal((5632L * 1024, 4_122_112L), (total, used));
    }

    [Fact, Trait("Lane", "unit")]
    public void Swap_usage_reads_a_comma_decimal_and_a_gigabyte_unit()
    {
        var (total, used) = MacFormat.SwapUsage("total = 2,00G  used = 0,00M  free = 2048,00M  (encrypted)");

        Assert.Equal((2L * 1024 * 1024, 0L), (total, used));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_swap_is_zero_total() => Assert.Equal((0L, 0L), MacFormat.SwapUsage("total = 0.00M  used = 0.00M  free = 0.00M  (encrypted)"));

    [Fact, Trait("Lane", "unit")]
    public void Swap_usage_that_is_not_swap_usage_is_a_format_error() => Assert.Throws<FormatException>(() => MacFormat.SwapUsage("sysctl: unknown oid"));

    [Fact, Trait("Lane", "unit")]
    public void Processes_are_read_with_parent_age_memory_and_the_whole_command_line()
    {
        var ps = "    1     0 06-20:15:23  22512 Ss   /sbin/launchd\n"
            + "45366 84627       00:38   1216 S    sleep 60\n"
            + "47876 67645    1:02:03   2608 Ss   /bin/zsh -c echo  two  spaces\n";

        var processes = MacFormat.Processes(ps);

        Assert.Equal(
            [(1, 0, "/sbin/launchd", 22512L, 591_323L), (45366, 84627, "sleep 60", 1216L, 38L), (47876, 67645, "/bin/zsh -c echo  two  spaces", 2608L, 3723L)],
            processes.Select(p => (p.Pid, p.Ppid, p.Cmdline, p.RssKb, p.AgeSeconds)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Zombies_and_lines_that_are_not_processes_are_left_out()
    {
        var ps = "  100     1       00:10      0 Z    (defunct)\nnot a process line\n  200     1       00:10    500 S    /bin/ok\n\n";

        Assert.Equal([200], MacFormat.Processes(ps).Select(p => p.Pid));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_process_with_no_arguments_is_listed_by_its_pid_in_brackets() =>
        Assert.Equal("[300]", Assert.Single(MacFormat.Processes("  300     1       00:10    500 S    ")).Cmdline);
}
