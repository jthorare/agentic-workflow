using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Todos;

/// <summary>'sindri todo note set' and 'stop' as Program dispatches them, with no control plane (todo spec §4.4, §4.5, §9 TP-13): one validated write of the profile's 'todo.obsidian', live at once, for a human and for a launched session.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class TodoNoteSetupCommandTests : IDisposable
{
    private const string Profile = "# mine\nschemaVersion: 1\nuser: me\ncontext:\n  sources:\n    library:\n      vaults:\n        - { name: notes, path: /v/notes, deny: [\"private/**\"] }\n        - { name: work, path: /v/work }\nreview:\n  login: octocat   # me\n";
    private const string WithKey = Profile + "todo:\n  obsidian:\n    vault: \"notes\"\n    note: \"Todo.md\"\n";

    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private string? _launch;

    public TodoNoteSetupCommandTests()
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

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_LAUNCH" => _launch, _ => null };

    private Task<int> Run(params string[] args) => CommandTable.Run(args, Environment, TimeProvider.System);

    private Task<string> Profile_() => File.ReadAllTextAsync(ProfilePath, TestContext.Current.CancellationToken);

    [Fact, Trait("Lane", "integration")]
    public async Task Set_writes_both_fields_in_one_edit_keeps_the_rest_of_the_file_and_says_what_it_set()
    {
        var code = await Run("todo", "note", "set", "notes", "Todo.md");

        Assert.Equal(0, code);
        Assert.Equal(WithKey, await Profile_());
        Assert.Contains("todo.obsidian set to notes/Todo.md", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Set_again_with_the_same_values_is_unchanged_exit_0_and_leaves_the_file_alone()
    {
        await File.WriteAllTextAsync(ProfilePath, WithKey, TestContext.Current.CancellationToken);

        var code = await Run("todo", "note", "set", "notes", "Todo.md");

        Assert.Equal(0, code);
        Assert.Equal(WithKey, await Profile_());
        Assert.Contains("unchanged", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Set_with_another_note_changes_only_the_note_line()
    {
        await File.WriteAllTextAsync(ProfilePath, WithKey, TestContext.Current.CancellationToken);

        var code = await Run("todo", "note", "set", "notes", "Daily/Todo.md");

        Assert.Equal(0, code);
        Assert.Equal(WithKey.Replace("\"Todo.md\"", "\"Daily/Todo.md\"", StringComparison.Ordinal), await Profile_());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("elsewhere", "Todo.md", "todo.obsidian.vault")]
    [InlineData("notes", "../Todo.md", "todo.obsidian.note")]
    [InlineData("notes", "/etc/Todo.md", "todo.obsidian.note")]
    [InlineData("notes", "Todo.txt", "todo.obsidian.note")]
    [InlineData("notes", ".obsidian/Todo.md", "todo.obsidian.note")]
    [InlineData("notes", "private/Todo.md", "todo.obsidian.note")]
    public async Task Set_with_a_bad_value_is_SND_TODO_002_with_the_key_path_exit_2_and_the_file_is_untouched(string vault, string note, string key)
    {
        var code = await Run("todo", "note", "set", vault, note);

        Assert.Equal(2, code);
        Assert.Contains("SND-TODO-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains(key, _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(Profile, await Profile_());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_launched_session_can_set_and_stop_because_the_todo_writes_are_request_class()
    {
        _launch = "lau_test";

        Assert.Equal(0, await Run("todo", "note", "set", "work", "Todo.md"));
        Assert.Contains("vault: \"work\"", await Profile_(), StringComparison.Ordinal);
        Assert.Equal(0, await Run("todo", "note", "stop"));
        Assert.Equal(Profile, await Profile_());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Stop_removes_the_key_with_its_parent_keeps_the_comments_and_prints_the_key_path()
    {
        await File.WriteAllTextAsync(ProfilePath, WithKey, TestContext.Current.CancellationToken);

        var code = await Run("todo", "note", "stop");

        Assert.Equal(0, code);
        Assert.Equal(Profile, await Profile_());
        Assert.Contains("todo.obsidian", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Stop_with_no_key_set_is_unchanged_exit_0_and_leaves_the_file_alone()
    {
        var code = await Run("todo", "note", "stop");

        Assert.Equal(0, code);
        Assert.Equal(Profile, await Profile_());
        Assert.Contains("unchanged", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Stop_in_a_layout_it_will_not_edit_is_SND_PROFILE_002_exit_2_and_the_file_is_untouched()
    {
        const string Flow = Profile + "todo: { obsidian: { vault: notes, note: Todo.md } }\n";
        await File.WriteAllTextAsync(ProfilePath, Flow, TestContext.Current.CancellationToken);

        var code = await Run("todo", "note", "stop");

        Assert.Equal(2, code);
        Assert.Contains("SND-PROFILE-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(Flow, await Profile_());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task With_no_profile_file_it_is_SND_PROFILE_001_exit_2()
    {
        File.Delete(ProfilePath);

        var code = await Run("todo", "note", "stop");

        Assert.Equal(2, code);
        Assert.Contains("SND-PROFILE-001", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("todo", "note", "set")]
    [InlineData("todo", "note", "set", "notes")]
    [InlineData("todo", "note", "set", "a", "b", "c")]
    [InlineData("todo", "note", "stop", "extra")]
    public async Task A_missing_or_extra_argument_is_a_usage_error_and_the_file_is_untouched(params string[] args)
    {
        Assert.Equal(2, await Run(args));
        Assert.Equal(Profile, await Profile_());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_prints_the_path_vault_note_and_whether_it_changed_for_set_and_the_keys_for_stop()
    {
        Assert.Equal(0, await Run("todo", "note", "set", "notes", "Todo.md", "--json"));
        var set = _out.ToString().Replace("\":\"", "\": \"", StringComparison.Ordinal);
        Assert.Contains("\"vault\": \"notes\"", set, StringComparison.Ordinal);
        Assert.Contains("\"note\": \"Todo.md\"", set, StringComparison.Ordinal);
        Assert.Contains("\"changed\"", set, StringComparison.Ordinal);

        _ = _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run("todo", "note", "stop", "--json"));
        Assert.Contains("todo.obsidian", _out.ToString(), StringComparison.Ordinal);
    }
}
