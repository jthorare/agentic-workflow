using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>'sindri index connect' is no longer the human's alone: an agent session that names its token's environment variable gets past the caller check and is judged on the profile and the token like the owner.</summary>
[Collection(ProcessEnvironment.Name), Trait("Lane", "integration")]
public sealed class IndexConnectVerbTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public IndexConnectVerbTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private Func<string, string?> Environment(string? launch) => name => name switch
    {
        "AW_SINDRI_HOME" => _home.Path,
        "AW_SINDRI_LAUNCH" => launch,
        "SOURCE_TOKEN_FOR_TEST" => "a-token-value",
        _ => null,
    };

    [Theory]
    [InlineData("github", null)]
    [InlineData("slack", null)]
    [InlineData("linear", null)]
    [InlineData("github", "LCH-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task An_agent_session_may_connect_a_source_with_a_token_pointer(string source, string? launch)
    {
        var exit = await CommandTable.Run(["index", "connect", source, "--token-env", "SOURCE_TOKEN_FOR_TEST"], Environment(launch), TimeProvider.System);

        // No profile in the scratch home, so the connect stops at the profile: the point is that it got that far.
        Assert.Equal(2, exit);
        Assert.DoesNotContain("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("a-token-value", _error.ToString() + _out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_token_pointer_to_an_unset_variable_is_a_usage_error_naming_the_variable()
    {
        var exit = await CommandTable.Run(["index", "connect", "github", "--token-env", "NO_SUCH_VARIABLE"], Environment(launch: null), TimeProvider.System);

        Assert.Equal(2, exit);
        Assert.Contains("NO_SUCH_VARIABLE", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
    }
}
