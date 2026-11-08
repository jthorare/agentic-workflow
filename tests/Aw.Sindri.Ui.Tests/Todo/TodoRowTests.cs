using System.Globalization;
using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Todo.TodoFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>A todo row's chips, tags, due word and accessible sentence (todo spec §3.1, §3.5), read from the golden on 2026-01-01 (a Thursday).</summary>
[Lane(TestLane.Unit)]
public sealed class TodoRowTests
{
    private readonly TodoFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_due_words_carry_text_for_overdue_today_this_week_and_later()
    {
        var panel = await _f.Read(Token);

        string Due(string text) => panel.Rows.Single(r => r.Text.StartsWith(text, StringComparison.Ordinal)).DueWord;
        Assert.Equal("Overdue · Wed", Due("rotate"));
        Assert.Equal("Today", Due("check the flaky"));
        Assert.Equal("Fri", Due("reply to Sam"));
        Assert.Equal("01-09", Due("answer"));
        Assert.Equal(string.Empty, Due("call the dentist"));
    }

    [Fact]
    public async Task Only_an_overdue_open_row_is_overdue()
    {
        var panel = await _f.Read(Token);

        Assert.Equal(["rotate the staging key"], panel.Rows.Where(r => r.IsOverdue).Select(r => r.Text), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_todo_added_by_a_session_or_through_a_pipe_is_tagged_agent()
    {
        var panel = await _f.Read(Token);

        string Tag(string text) => panel.Rows.Single(r => r.Text.StartsWith(text, StringComparison.Ordinal)).Tag;
        Assert.Equal("agent", Tag("check the flaky"));   // addedBy session:mcp
        Assert.Equal("agent", Tag("answer"));            // addedVia pipe, then edited by a session
        Assert.Equal(string.Empty, Tag("rotate"));       // human:cli, a terminal
        Assert.Equal(string.Empty, Tag("call the dentist"));   // obsidian
    }

    [Fact]
    public async Task An_agent_editing_the_owners_todo_tags_it_edited_by_an_agent()
    {
        var edited = Of(1);
        edited["lastBy"] = "session:cli:launch-7";
        _f.ServeTodos(edited);

        var panel = await _f.Read(Token);

        Assert.Equal("edited by an agent", panel.Rows[0].Tag);
    }

    [Fact]
    public async Task The_owner_fixing_an_agents_todo_leaves_it_tagged_agent()
    {
        var panel = await _f.Read(Token);

        var flaky = panel.Rows.Single(r => r.Text.StartsWith("check the flaky", StringComparison.Ordinal));
        Assert.Equal("agent", flaky.Tag);   // addedBy session:mcp, lastBy human:ui
    }

    [Fact]
    public async Task A_row_draws_three_link_chips_then_the_rest_as_a_count()
    {
        var many = Of(2);
        many["links"] = new System.Text.Json.Nodes.JsonArray(Enumerable.Range(1, 5).Select(i =>
            (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject { ["kind"] = "linear", ["ref"] = string.Create(CultureInfo.InvariantCulture, $"ENG-{i}"), ["href"] = string.Create(CultureInfo.InvariantCulture, $"sindri://linear/ENG-{i}") }).ToArray());
        _f.ServeTodos(many, Of(1));

        var panel = await _f.Read(Token);

        Assert.Equal(["ENG-1", "ENG-2", "ENG-3"], panel.Rows[0].Chips, StringComparer.Ordinal);
        Assert.Equal("+2", panel.Rows[0].MoreChips);
        Assert.Empty(panel.Rows[1].Chips);
        Assert.Equal(string.Empty, panel.Rows[1].MoreChips);
    }

    [Fact]
    public async Task Two_links_draw_two_chips_and_no_count()
    {
        var panel = await _f.Read(Token);

        var rotate = panel.Rows.Single(r => r.Text.StartsWith("rotate", StringComparison.Ordinal));
        Assert.Equal(["acme/api#12", "ENG-12"], rotate.Chips, StringComparer.Ordinal);
        Assert.Equal(string.Empty, rotate.MoreChips);
    }

    [Fact]
    public async Task The_accessible_sentence_names_the_text_the_day_and_the_state()
    {
        var panel = await _f.Read(Token);

        string Name(string text) => panel.Rows.Single(r => r.Text.StartsWith(text, StringComparison.Ordinal)).AccessibleName;
        Assert.Equal("Todo, reply to Sam about the offsite, due Friday, not done", Name("reply to Sam"));
        Assert.Equal("Todo, rotate the staging key, overdue, due Wednesday, not done, 2 links", Name("rotate"));
        Assert.Equal("Todo, check the flaky sync test, due today, not done, 1 link, added by an agent", Name("check the flaky"));
        Assert.Equal("Todo, call the dentist, no due date, not done", Name("call the dentist"));
        Assert.Equal("Todo, answer the review comments on acme/web#40, due January 9, not done, 1 link, added by an agent", Name("answer"));
    }

    [Fact]
    public async Task A_done_row_reads_done_and_a_removed_one_reads_removed()
    {
        var panel = await _f.Expanded(Token);
        panel.ToggleDone();

        var done = panel.Groups.Single(g => g.Heading == "Done").Rows.Single();
        var removed = panel.Groups.Single(g => g.Heading == "Removed").Rows.Single();

        Assert.Equal(("Todo, renew the cert, due Tuesday, done", true), (done.AccessibleName, done.IsDone));
        Assert.Equal(("Todo, book the offsite room, no due date, removed", true), (removed.AccessibleName, removed.IsRemoved));
        Assert.False(done.IsOverdue);   // only an open todo is overdue
    }
}
