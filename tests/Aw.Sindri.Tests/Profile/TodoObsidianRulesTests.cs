using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>'todo.obsidian' (todo spec §4.5, §6): the vault is one of the library's, the note a vault-relative .md path that stays inside it; a bad value is SND-TODO-002 with the key path.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoObsidianRulesTests
{
    private const string Context = """{"sources":{"library":{"vaults":[{"name":"notes","path":"/v/notes","deny":["private/**","*.secret.md"]},{"name":"work","path":"/v/work"}]}}}""";

    private static SindriProfile With(string? vault, string? note, string context = Context)
    {
        using var doc = JsonDocument.Parse(context);
        return new SindriProfile(1, "thor", Context: doc.RootElement.Clone(), Todo: new TodoSettings(new TodoObsidianSettings(vault, note)));
    }

    [Fact]
    public void A_profile_without_the_key_is_accepted() => Assert.True(ProfileRules.Check(new SindriProfile(1, "thor")).IsSuccess);

    [Theory]
    [InlineData("notes", "Todo.md")]
    [InlineData("work", "Daily/Todo.md")]
    [InlineData("notes", "a/b/c/list.MD")]
    public void A_vault_of_the_library_and_a_relative_md_path_are_accepted(string vault, string note) =>
        Assert.True(ProfileRules.Check(With(vault, note)).IsSuccess);

    [Fact]
    public void An_unknown_vault_is_SND_TODO_002_naming_todo_obsidian_vault_and_the_vaults_there_are()
    {
        var error = ProfileRules.Check(With("elsewhere", "Todo.md")).Error;

        Assert.Equal("SND-TODO-002", error.Code);
        Assert.Contains("todo.obsidian.vault", error.Message, StringComparison.Ordinal);
        Assert.Contains("notes, work", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_with_no_library_vaults_says_to_add_one_first()
    {
        var error = ProfileRules.Check(With("notes", "Todo.md", context: """{"sources":{}}""")).Error;

        Assert.Equal("SND-TODO-002", error.Code);
        Assert.Contains("no library vaults", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_vault_is_refused_naming_todo_obsidian_vault(string? vault)
    {
        var error = ProfileRules.Check(With(vault, "Todo.md")).Error;

        Assert.Equal("SND-TODO-002", error.Code);
        Assert.Contains("todo.obsidian.vault", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/etc/Todo.md")]
    [InlineData("C:/Todo.md")]
    [InlineData("../Todo.md")]
    [InlineData("a/../../Todo.md")]
    [InlineData("a/./Todo.md")]
    [InlineData("a\\Todo.md")]
    [InlineData("a//Todo.md")]
    [InlineData("Todo.txt")]
    [InlineData(".md")]
    [InlineData("notes/")]
    [InlineData(".obsidian/Todo.md")]
    [InlineData("a/.hidden/Todo.md")]
    public void A_note_path_that_is_not_a_relative_md_file_inside_the_vault_is_refused_naming_todo_obsidian_note(string? note)
    {
        var error = ProfileRules.Check(With("notes", note)).Error;

        Assert.Equal("SND-TODO-002", error.Code);
        Assert.Contains("todo.obsidian.note", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private/Todo.md")]
    [InlineData("x.secret.md")]
    public void A_note_matched_by_the_vaults_deny_globs_is_refused(string note)
    {
        var error = ProfileRules.Check(With("notes", note)).Error;

        Assert.Equal("SND-TODO-002", error.Code);
        Assert.Contains("deny glob", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Another_vaults_deny_globs_do_not_apply() => Assert.True(ProfileRules.Check(With("work", "private/Todo.md")).IsSuccess);

    [Fact]
    public void A_dot_file_name_in_a_normal_folder_is_still_a_md_file_but_a_bare_dot_md_is_not() =>
        Assert.Null(TodoObsidianRules.NotePath("a/.Todo.md"));

    [Theory]
    [InlineData("Private/**", "private/Todo.md", true, true)]
    [InlineData("Private/**", "private/Todo.md", false, false)]
    [InlineData("*.SECRET.md", "x.secret.md", true, true)]
    [InlineData("*.SECRET.md", "x.secret.md", false, false)]
    [InlineData("private/**", "private/Todo.md", false, true)]
    public void A_deny_glob_ignores_case_only_where_the_platform_does(string glob, string path, bool ignoreCase, bool matches) =>
        Assert.Equal(matches, PathGlob.Matches(glob, path, ignoreCase));

    [Theory]
    [InlineData("a:b.md", true, "must be relative to the vault, not an absolute path")]
    [InlineData("a:b.md", false, null)]
    [InlineData("C:/Todo.md", false, "must be relative to the vault, not an absolute path")]
    [InlineData("C:/Todo.md", true, "must be relative to the vault, not an absolute path")]
    public void A_drive_letter_is_refused_for_a_colon_name_only_on_windows(string note, bool windows, string? reason) =>
        Assert.Equal(reason, TodoObsidianRules.NotePath(note, windows));
}
