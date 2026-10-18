using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class MacToolsTests
{
    private const int Large = 200_000;

    [Fact, Trait("Lane", "integration")]
    public async Task A_tool_s_whole_output_is_returned_however_long_it_is()
    {
        var output = await MacTools.Output(new SystemCommandRunner(), ["sh", "-c", "head -c 200000 /dev/zero | tr '\\0' x"], TestContext.Current.CancellationToken);

        Assert.True(output.IsSuccess);
        Assert.Equal(Large, output.Value.Length);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tool_that_exits_non_zero_is_AW_META_010_naming_it_as_unavailable_on_macOS()
    {
        var output = await MacTools.Output(new SystemCommandRunner(), ["sh", "-c", "echo boom >&2; exit 3"], TestContext.Current.CancellationToken);

        Assert.True(output.IsError);
        Assert.Equal(MetaErrors.HostUnreadable.Code, output.Error.Code);
        Assert.Contains("unavailable on macOS: sh -c echo boom >&2; exit 3 exited 3: boom", output.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tool_that_is_not_installed_is_AW_META_010_naming_it_as_unavailable_on_macOS()
    {
        var output = await MacTools.Output(new SystemCommandRunner(), ["aw-meta-no-such-tool"], TestContext.Current.CancellationToken);

        Assert.True(output.IsError);
        Assert.Contains("unavailable on macOS: aw-meta-no-such-tool could not be run", output.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_tool_s_stderr_in_the_error_is_cut_to_about_300_characters()
    {
        var noisy = new InMemoryCommandRunner().When("noisy", _ => new CommandResult(1, string.Empty, new string('x', 5000), TimedOut: false));

        var output = await MacTools.Output(noisy, ["noisy"], TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, output.Error.Code);
        Assert.InRange(output.Error.Message.Length, 1, 400);
        Assert.Contains("(truncated)", output.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_short_stderr_is_kept_whole()
    {
        var runner = new InMemoryCommandRunner().When("short", _ => new CommandResult(2, string.Empty, "  boom \n", TimedOut: false));

        var output = await MacTools.Output(runner, ["short"], TestContext.Current.CancellationToken);

        Assert.EndsWith("exited 2: boom", output.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_tail_asked_for_is_the_documented_16_MiB_cap_not_int_MaxValue()
    {
        var runner = new InMemoryCommandRunner().When("big", "ok");

        _ = await MacTools.Output(runner, ["big"], TestContext.Current.CancellationToken);

        Assert.Equal(16 * 1024 * 1024, Assert.Single(runner.Requests).TailCharacters);
        Assert.Equal(16 * 1024 * 1024, MacTools.OutputCharacterCap);
    }
}
