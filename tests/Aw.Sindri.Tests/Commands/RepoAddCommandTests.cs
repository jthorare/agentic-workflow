using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri repo add' as Program dispatches it, next to 'repo status': add reaches the add verb (and, with no control plane, says so), status is untouched, and anything else under 'repo' is an unknown verb.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class RepoAddCommandTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public RepoAddCommandTests()
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

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, _ => null };

    private Task<int> Run(params string[] args) => CommandTable.Run(args, Environment, TimeProvider.System);

    [Fact, Trait("Lane", "integration")]
    public async Task Repo_add_with_no_control_plane_is_exit_1_and_says_sindri_is_not_running()
    {
        var code = await Run("repo", "add", "/work/demo", "--detach");

        Assert.Equal(1, code);
        Assert.Contains("SND-API-001", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Repo_add_without_a_path_is_the_usage_error()
    {
        var code = await Run("repo", "add");

        Assert.Equal(2, code);
        Assert.Contains(RepoVerbs.Usage, _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Repo_status_still_dispatches_to_the_status_verb_and_an_unknown_repo_verb_is_refused()
    {
        var status = await Run("repo", "status", "/elsewhere", "--nudge");
        var unknown = await Run("repo", "bogus");

        Assert.Equal(0, status);
        Assert.Equal(2, unknown);
    }
}
