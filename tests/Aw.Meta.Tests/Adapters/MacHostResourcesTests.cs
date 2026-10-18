using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

/// <summary>MacHostResources through a fake command runner, so they run on any OS.</summary>
public sealed class MacHostResourcesTests
{
    private const long DiskFree = 123_456_789;
    private static readonly string[] _inOrder = ["/usr/sbin/sysctl -n hw.memsize", "/usr/bin/vm_stat", "/usr/sbin/sysctl -n vm.swapusage"];

    private static (MacHostResources Resources, InMemoryCommandRunner Runner) Make(Func<InMemoryCommandRunner, InMemoryCommandRunner>? script = null)
    {
        var runner = new InMemoryCommandRunner();
        _ = (script ?? (r => r.When("hw.memsize", MacOutput.MemSize).When("vm_stat", MacOutput.VmStat).When("vm.swapusage", MacOutput.Swap)))(runner);
        var reader = new MacHostReader("/state", runner, MacToolPaths.Default, _ => DiskFree);
        return (new MacHostResources(reader, "/state/locks/heavy-job.lock", new InMemoryProcessLiveness()), runner);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_tools_run_in_order_memory_total_then_vm_stat_then_swap()
    {
        var (resources, runner) = Make();

        _ = await resources.Read(TestContext.Current.CancellationToken);

        Assert.Equal(_inOrder, runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_figures_are_mapped_from_the_tools_output_and_the_disk()
    {
        var (resources, _) = Make();

        var read = await resources.Read(TestContext.Current.CancellationToken);

        Assert.True(read.IsSuccess);
        Assert.Equal((50_331_648L, 1_391_046L * 16, 4_122_112L, DiskFree, 5632L * 1024), (read.Value.MemTotalKb, read.Value.MemAvailableKb, read.Value.SwapUsedKb, read.Value.DiskFreeBytes, read.Value.SwapTotalKb));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_every_tool_fails_the_first_one_s_error_wins_and_the_rest_do_not_run()
    {
        var (resources, runner) = Make(r => r.When("hw.memsize", "", exitCode: 1).When("vm_stat", "", exitCode: 1).When("vm.swapusage", "", exitCode: 1));

        var read = await resources.Read(TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, read.Error.Code);
        Assert.Contains("sysctl -n hw.memsize exited 1", read.Error.Message, StringComparison.Ordinal);
        Assert.Equal(["/usr/sbin/sysctl -n hw.memsize"], runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Of_vm_stat_and_swap_failing_vm_stat_s_error_wins()
    {
        var (resources, _) = Make(r => r.When("hw.memsize", MacOutput.MemSize).When("vm_stat", "", exitCode: 1).When("vm.swapusage", "", exitCode: 1));

        var read = await resources.Read(TestContext.Current.CancellationToken);

        Assert.Contains("vm_stat exited 1", read.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tool_that_is_not_installed_is_AW_META_010_unavailable_on_macOS()
    {
        var (resources, _) = Make(r => r);

        var read = await resources.Read(TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, read.Error.Code);
        Assert.Contains("unavailable on macOS: /usr/sbin/sysctl -n hw.memsize could not be run", read.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("vm_stat", "no page size here\n", "unavailable on macOS: vm_stat reported no page size")]
    [InlineData("vm.swapusage", "sysctl: unknown oid\n", "unavailable on macOS: vm.swapusage has no \"total\" figure")]
    [InlineData("hw.memsize", "lots\n", "unavailable on macOS: ")]
    public async Task Output_that_cannot_be_read_is_AW_META_010_unavailable_on_macOS(string tool, string output, string message)
    {
        var (resources, _) = Make(r => r
            .When("hw.memsize", tool == "hw.memsize" ? output : MacOutput.MemSize)
            .When("vm_stat", tool == "vm_stat" ? output : MacOutput.VmStat)
            .When("vm.swapusage", tool == "vm.swapusage" ? output : MacOutput.Swap));

        var read = await resources.Read(TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, read.Error.Code);
        Assert.Contains(message, read.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("hw.memsize", "99999999999999999999\n")]
    [InlineData("vm_stat", "Mach Virtual Memory Statistics: (page size of 99999999999999999999 bytes)\nPages free: 1.\nPages inactive: 1.\nPages speculative: 1.\n")]
    [InlineData("vm_stat", "Mach Virtual Memory Statistics: (page size of 4611686018427387904 bytes)\nPages free: 9.\nPages inactive: 9.\nPages speculative: 9.\n")]
    [InlineData("vm.swapusage", "total = 99999999999999999999.00G  used = 0.00M  free = 0.00M\n")]
    public async Task A_number_too_large_for_a_long_is_AW_META_010_not_an_exception(string tool, string output)
    {
        var (resources, _) = Make(r => r
            .When("hw.memsize", tool == "hw.memsize" ? output : MacOutput.MemSize)
            .When("vm_stat", tool == "vm_stat" ? output : MacOutput.VmStat)
            .When("vm.swapusage", tool == "vm.swapusage" ? output : MacOutput.Swap));

        var read = await resources.Read(TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, read.Error.Code);
        Assert.Contains("unavailable on macOS: ", read.Error.Message, StringComparison.Ordinal);
    }
}
