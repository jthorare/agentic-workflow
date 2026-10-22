using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.InitProfile;

/// <summary>'sindri profile init' as Program dispatches it, with no control plane: the first run writes the profile and says what comes next, the second is refused and changes nothing.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class ProfileInitCommandTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public ProfileInitCommandTests()
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

    private string ProfilePath => Path.Combine(_home.Path, "profile", "profile.yaml");

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, _ => null };

    private Task<int> Run(params string[] args) => CommandTable.Run(args, Environment, TimeProvider.System);

    [Fact, Trait("Lane", "integration")]
    public async Task The_first_run_writes_the_profile_and_names_the_next_steps_and_the_second_is_refused()
    {
        var first = await Run("profile", "init");
        var written = await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken);
        var second = await Run("profile", "init");

        Assert.Equal(0, first);
        Assert.Contains(ProfilePath, _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("sindri repo add <path>, then sindri profile approve", _out.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, second);
        Assert.Contains("SND-PROFILE-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(written, await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_prints_the_path()
    {
        var code = await Run("profile", "init", "--json");

        Assert.Equal(0, code);
        Assert.Contains("\"path\"", _out.ToString(), StringComparison.Ordinal);
    }
}
