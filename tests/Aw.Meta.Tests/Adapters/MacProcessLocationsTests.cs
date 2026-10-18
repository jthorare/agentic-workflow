using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

/// <summary>MacProcessLocations through a fake command runner, so it runs on any OS.</summary>
public sealed class MacProcessLocationsTests
{
    // init(1) > launcher(100) > wrapper shell(200) > aw-meta(300, the scanner); unrelated: dotnet(400), a sibling sh(500) of the wrapper's child
    private const string Lsof =
        "p100\nR1\ncinit-wrapper\nfcwd\nn/\n"
        + "p200\nR100\ncbash\nfcwd\nn/work/repo\n"
        + "p300\nR200\ncaw-meta\nfcwd\nn/work/repo\nf3\nn/work/repo/log\n"
        + "p400\nR1\ncdotnet\nfcwd\nn/work/repo\n"
        + "p500\nR1\ncsh\nfcwd\nn/\n";

    private static MacProcessLocations Make(InMemoryCommandRunner runner, int self = 300) =>
        new(new MacHostReader("/state", runner, MacToolPaths.Default, _ => 0), self);

    [Fact, Trait("Lane", "unit")]
    public async Task Every_process_lsof_lists_comes_back_with_its_command_and_paths()
    {
        var locations = await Make(new InMemoryCommandRunner().When("lsof", Lsof)).Locations(TestContext.Current.CancellationToken);

        var scanner = Assert.Single(locations.Value, p => p.Pid == 300);
        Assert.Equal("aw-meta", scanner.Command);
        Assert.Equal(["/work/repo", "/work/repo/log"], scanner.Paths);
        Assert.Equal(5, locations.Value.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_scanning_process_and_every_ancestor_up_to_init_are_invokers_and_nothing_else_is()
    {
        var locations = await Make(new InMemoryCommandRunner().When("lsof", Lsof)).Locations(TestContext.Current.CancellationToken);

        Assert.Equal([100, 200, 300], [.. locations.Value.Where(p => p.IsInvoker).Select(p => p.Pid).Order()]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_parent_loop_in_the_output_ends_the_walk()
    {
        var runner = new InMemoryCommandRunner().When("lsof", "p300\nR200\ncaw-meta\nfcwd\nn/\np200\nR300\ncbash\nfcwd\nn/\n");

        var locations = await Make(runner).Locations(TestContext.Current.CancellationToken);

        Assert.Equal([200, 300], [.. locations.Value.Where(p => p.IsInvoker).Select(p => p.Pid).Order()]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lsof_runs_by_its_absolute_path_with_the_tool_timeout_and_output_cap()
    {
        var runner = new InMemoryCommandRunner().When("lsof", Lsof);

        _ = await Make(runner).Locations(TestContext.Current.CancellationToken);

        var request = Assert.Single(runner.Requests);
        Assert.Equal(["/usr/sbin/lsof", "-w", "-n", "-F", "pcnR"], request.Argv);
        Assert.Equal((TimeSpan.FromSeconds(15), MacTools.OutputCharacterCap), (request.Timeout, request.TailCharacters));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_lsof_that_cannot_run_or_lists_nothing_is_AW_META_010_so_a_caller_fails_closed()
    {
        var missing = await Make(new InMemoryCommandRunner()).Locations(TestContext.Current.CancellationToken);
        var failed = await Make(new InMemoryCommandRunner().When("lsof", "", exitCode: 1)).Locations(TestContext.Current.CancellationToken);
        var empty = await Make(new InMemoryCommandRunner().When("lsof", "")).Locations(TestContext.Current.CancellationToken);

        foreach (var error in new[] { missing.Error, failed.Error, empty.Error })
        {
            Assert.Equal(MetaErrors.HostUnreadable.Code, error.Code);
            Assert.Contains("unavailable on macOS", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_lsof_whose_output_reaches_the_cap_is_AW_META_010_not_a_partial_process_list()
    {
        var full = Lsof + new string('x', MacTools.OutputCharacterCap);
        var locations = await Make(new InMemoryCommandRunner().When("lsof", full)).Locations(TestContext.Current.CancellationToken);

        Assert.True(locations.IsError);
        Assert.Equal(MetaErrors.HostUnreadable.Code, locations.Error.Code);
        Assert.Contains("unavailable on macOS: lsof output over the cap", locations.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_lsof_that_exits_1_with_output_is_usable_because_it_could_not_read_some_files()
    {
        var locations = await Make(new InMemoryCommandRunner().When("lsof", Lsof, exitCode: 1)).Locations(TestContext.Current.CancellationToken);

        Assert.True(locations.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_lsof_that_exits_2_with_output_is_still_AW_META_010()
    {
        var locations = await Make(new InMemoryCommandRunner().When("lsof", Lsof, exitCode: 2)).Locations(TestContext.Current.CancellationToken);

        Assert.True(locations.IsError);
    }
}
