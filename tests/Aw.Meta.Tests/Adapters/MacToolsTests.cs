using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Infrastructure.Processes;

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
}
