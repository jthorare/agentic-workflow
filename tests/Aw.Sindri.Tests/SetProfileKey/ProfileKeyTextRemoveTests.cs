using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Tests.SetProfileKey;

/// <summary>The key removal behind 'todo note stop' (todo spec §4.5 "Stopping"): the key's lines go, every other byte stays, an emptied parent goes, an absent key is unchanged.</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileKeyTextRemoveTests
{
    private const string Key = "todo.obsidian";

    [Fact]
    public void A_map_key_goes_with_its_block_and_the_comments_and_order_around_it_stay()
    {
        const string Before = "# mine\nuser: me\ntodo:\n  obsidian:\n    vault: notes\n    note: Todo.md\n  other: 1\nreview:\n  login: octocat   # me\n";

        var edited = ProfileKeyText.Remove(Before, Key);

        Assert.Equal("# mine\nuser: me\ntodo:\n  other: 1\nreview:\n  login: octocat   # me\n", edited.Value);
    }

    [Fact]
    public void A_parent_left_with_nothing_under_it_goes_too()
    {
        const string Before = "user: me\ntodo:\n  obsidian:\n    vault: notes\n    note: Todo.md\nreview:\n  login: octocat\n";

        var edited = ProfileKeyText.Remove(Before, Key);

        Assert.Equal("user: me\nreview:\n  login: octocat\n", edited.Value);
    }

    [Fact]
    public void The_last_block_of_the_file_is_removed_with_its_parent()
    {
        var edited = ProfileKeyText.Remove("user: me\ntodo:\n  obsidian:\n    vault: notes\n    note: Todo.md\n", Key);

        Assert.Equal("user: me\n", edited.Value);
    }

    [Fact]
    public void A_comment_between_the_block_and_the_next_key_stays()
    {
        const string Before = "todo:\n  obsidian:\n    vault: notes\n    note: Todo.md\n  # keep me\n  other: 1\n";

        var edited = ProfileKeyText.Remove(Before, Key);

        Assert.Equal("todo:\n  # keep me\n  other: 1\n", edited.Value);
    }

    [Fact]
    public void A_scalar_key_is_removed_on_its_own_line() =>
        Assert.Equal("review:\n  settings: x\n", ProfileKeyText.Remove("review:\n  login: octocat\n  settings: x\n", "review.login").Value);

    [Theory]
    [InlineData("user: me\n")]
    [InlineData("user: me\ntodo:\n  other: 1\n")]
    [InlineData("")]
    public void An_absent_key_or_parent_leaves_the_text_as_it_was(string text) =>
        Assert.Equal(text, ProfileKeyText.Remove(text, Key).Value);

    [Fact]
    public void Carriage_returns_are_kept_on_the_lines_that_stay()
    {
        var edited = ProfileKeyText.Remove("user: me\r\ntodo:\r\n  obsidian:\r\n    vault: notes\r\nreview: x\r\n", Key);

        Assert.Equal("user: me\r\nreview: x\r\n", edited.Value);
    }

    [Fact]
    public void A_flow_style_key_is_a_layout_it_will_not_remove()
    {
        var edited = ProfileKeyText.Remove("todo:\n  obsidian: { vault: notes, note: Todo.md }\n", Key);

        Assert.Equal("SND-PROFILE-002", edited.Error.Code);
    }

    [Fact]
    public void A_flow_style_parent_is_refused()
    {
        var edited = ProfileKeyText.Remove("todo: { obsidian: { vault: notes } }\n", Key);

        Assert.Equal("SND-PROFILE-002", edited.Error.Code);
    }

    [Fact]
    public void A_bad_key_is_SND_CLI_003() =>
        Assert.Equal("SND-CLI-003", ProfileKeyText.Remove("user: me\n", "todo..obsidian").Error.Code);
}
