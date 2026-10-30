using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.SetProfileKey;

/// <summary>'sindri profile set' as Program dispatches it, with no control plane (#1740): the edit is checked before it is written, a launched session is refused and nothing is ever approved.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class ProfileSetCommandTests : IDisposable
{
    private const string Profile = "# mine\nschemaVersion: 1\nuser: me\ncontext:\n  models:\n    labeling:\n      pick: off   # default\n";

    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private string? _launch;
    private bool _token = true;

    public ProfileSetCommandTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
        File.WriteAllText(ProfilePath, Profile);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string ProfilePath => Path.Combine(_home.Path, "profile", "profile.yaml");

    private void HumanToken()
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "auth"));
        File.WriteAllText(Path.Combine(_home.Path, "auth", "human.token"), "a-human-token\n");
    }

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_LAUNCH" => _launch, _ => null };

    private Task<int> Run(params string[] args)
    {
        if (_token)
        {
            HumanToken();
        }

        return CommandTable.Run(args, Environment, TimeProvider.System);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_valid_value_is_written_on_its_line_and_the_next_step_is_approving()
    {
        var code = await Run("profile", "set", "context.models.labeling.pick", "jev");

        Assert.Equal(0, code);
        Assert.Equal(Profile.Replace("pick: off", "pick: jev", StringComparison.Ordinal), await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken));
        Assert.Contains($"set context.models.labeling.pick = jev in {ProfilePath}. Approve it with: sindri profile approve", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_value_the_profile_reader_refuses_is_printed_with_its_key_path_and_the_file_is_untouched()
    {
        var code = await Run("profile", "set", "context.models.labeling.pick", "maybe");

        Assert.Equal(1, code);
        Assert.Contains("SND-CONTEXT-001", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("context.models.labeling.pick", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(Profile, await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_same_value_again_says_unchanged_and_leaves_the_file_alone()
    {
        var code = await Run("profile", "set", "context.models.labeling.pick", "off");

        Assert.Equal(0, code);
        Assert.Contains("unchanged", _out.ToString(), StringComparison.Ordinal);
        Assert.Equal(Profile, await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_layout_it_will_not_edit_is_exit_1_and_the_file_is_untouched()
    {
        var code = await Run("profile", "set", "context.models.labeling", "off");

        Assert.Equal(1, code);
        Assert.Contains("context.models.labeling", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(Profile, await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_launched_session_is_refused_SND_AUTH_002_and_the_file_is_untouched()
    {
        _launch = "lau_test";

        var code = await Run("profile", "set", "context.models.labeling.pick", "jev");

        Assert.Equal(2, code);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(Profile, await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_the_per_start_human_token_it_is_refused_SND_AUTH_002_and_the_file_is_untouched()
    {
        _token = false;

        var code = await Run("profile", "set", "context.models.labeling.pick", "jev");

        Assert.Equal(2, code);
        Assert.Contains("SND-AUTH-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(Profile, await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("profile", "set")]
    [InlineData("profile", "set", "context.models.labeling.pick")]
    [InlineData("profile", "set", "a", "b", "c")]
    public async Task A_missing_or_extra_argument_is_a_usage_error(params string[] args)
    {
        Assert.Equal(2, await Run(args));
        Assert.Equal(Profile, await File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_prints_the_path_key_value_and_whether_it_changed()
    {
        var code = await Run("profile", "set", "context.models.labeling.pick", "jev", "--json");

        Assert.Equal(0, code);
        var json = _out.ToString();
        Assert.Contains("\"path\"", json, StringComparison.Ordinal);
        Assert.Contains("\"key\": \"context.models.labeling.pick\"", json.Replace("\":\"", "\": \"", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("\"changed\"", json, StringComparison.Ordinal);
    }
}
