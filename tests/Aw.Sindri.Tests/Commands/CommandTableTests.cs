using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// The verb table as Program runs it, with the console swapped for writers: the exit codes the doc promises for the
/// local verbs. It swaps the process's console, so it never runs beside another test.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class CommandTableTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public CommandTableTests()
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

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_verb_exits_2_with_SND_CLI_001()
    {
        var code = await Run("frobnicate");

        Assert.Equal(2, code);
        Assert.Contains("SND-CLI-001 'frobnicate' is not a sindri command", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Index_embed_reaches_the_embed_verbs_not_the_index_read_verbs()
    {
        var code = await Run("index", "embed", "bogus");

        Assert.Equal(2, code);
        Assert.Contains("'index embed bogus' isn't an index embed command", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("none", 2)]
    [InlineData("invalid", 2)]
    [InlineData("valid", 0)]
    public async Task Profile_validate_exits_0_valid_2_invalid_or_missing(string state, int expected)
    {
        await Put(state);

        Assert.Equal(expected, await Run("profile", "validate"));
    }

    private async Task Put(string state)
    {
        if (state == "none")
        {
            return;
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);   // why: the profile path has a parent
        await File.WriteAllTextAsync(ProfilePath, state == "invalid" ? "schemaVersion: 1\nuser: thor\nunknownKey: 1\n" : "schemaVersion: 1\nuser: thor\n",
            TestContext.Current.CancellationToken);
    }

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, _ => null };

    private Task<int> Run(params string[] args) =>
        CommandTable.Run(args, Environment, TimeProvider.System);
}
