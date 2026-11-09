using System.Text.RegularExpressions;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>D3: a line of the note whose text a todo can't hold (a marker, a markdown link, a hidden character) never becomes a todo and is never rewritten.</summary>
public sealed partial class NoteBrokenLineSyncTests
{
    private const string Id = "01jc5b2kz8m3r6tq1w9xh4dn7y";

    private static async Task<SyncRig> Synced(ITextScrubber? scrubber = null)
    {
        var rig = await SyncRig.Start(scrubber);
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        _ = await rig.Pass();
        return rig;
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("- [ ] pay the \U0001F4C5 invoice")]
    [InlineData("- [ ] it was ✅ done")]
    [InlineData("- [ ] odd ^td-abc marker in the middle")]
    [InlineData("- [ ] see [the docs](https://example.com/a) first")]
    [InlineData("- [ ] ring the \u0007 bell")]
    [InlineData("- [ ] zero​width")]
    public async Task A_bare_line_a_todo_cannot_hold_is_not_adopted_and_is_left_as_typed(string line)
    {
        var rig = await Synced();
        rig.Files.Files[SyncRig.Path] += line + "\n";

        var status = await rig.Pass();

        Assert.Empty(await rig.Events(EventTypes.TodoAdded));
        Assert.EndsWith(line + "\n", rig.Note, StringComparison.Ordinal);
        Assert.Equal(TodoNoteState.Syncing, status.State);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("- [ ] pay the \U0001F4C5 invoice ^td-" + Id)]
    [InlineData("- [ ] see [the docs](https://example.com/a) first ^td-" + Id)]
    [InlineData("- [ ] ring the \u0007 bell ^td-" + Id)]
    public async Task A_line_with_an_id_nobody_knows_and_text_a_todo_cannot_hold_is_not_adopted(string line)
    {
        var rig = await Synced();
        rig.Files.Files[SyncRig.Path] += line + "\n";

        _ = await rig.Pass();

        Assert.Empty(await rig.Events(EventTypes.TodoAdded));
        Assert.EndsWith(line + "\n", rig.Note, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_known_todos_line_edited_into_a_broken_one_changes_nothing_on_either_side()
    {
        var rig = await Synced();
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("reply to Sam", "reply to [Sam](https://example.com)", StringComparison.Ordinal);
        var written = rig.Note;

        _ = await rig.Pass();

        Assert.Empty(await rig.Events(EventTypes.TodoChanged));
        Assert.Equal(written, rig.Note);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_good_line_beside_a_broken_one_is_still_adopted()
    {
        var rig = await Synced();
        rig.Files.Files[SyncRig.Path] += "- [ ] see [docs](https://example.com)\n- [ ] buy milk\n";

        _ = await rig.Pass();

        var added = Assert.Single(await rig.Payloads<TodoAdded>(EventTypes.TodoAdded));
        Assert.Equal("buy milk", added.Text);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("- [ ] key ghp_aaaaaaaaaaaaaaaaaaaaaaaa(see below)")]
    [InlineData("- [ ] key ghp_aaaaaaaaaaaaaaaaaaaaaaaa \U0001F4C5x")]
    public async Task Text_the_scrubber_turns_into_a_marker_or_link_is_not_adopted(string line)
    {
        var rig = await Synced(new MaskingScrubber());
        rig.Files.Files[SyncRig.Path] += line + "\n";

        _ = await rig.Pass();

        Assert.All(await rig.Payloads<TodoAdded>(EventTypes.TodoAdded), added => Assert.True(TodoText.Check(added.Text).IsSuccess, added.Text));
    }

    private sealed partial class MaskingScrubber : ITextScrubber
    {
        public string Scrub(string text) => Token.Replace(text, "[redacted]");

        [GeneratedRegex("ghp_[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        private static partial Regex Token { get; }
    }
}
