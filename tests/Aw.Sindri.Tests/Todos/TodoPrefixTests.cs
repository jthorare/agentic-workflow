using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The CLI's id prefixes (todo spec §4.4 "Ids"): per verb, among the todos the verb acts on; SND-TODO-010 when several match.</summary>
public sealed class TodoPrefixTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static TodoRow Row(string id, bool done = false, bool removed = false) =>
        new(TodoId.From(id), "x", Due: null, Links: [], _at, "human:ui", AddedVia: null, done ? _at : null, removed, removed ? _at : null, removed ? "human:ui" : null,
            LastLineHash: null, Version: 1, "human:ui", LastVia: null, Conflict: null, PickedConflict: null, Sequence: 1);

    private static readonly TodoRow[] _rows =
    [
        Row("td_01jc5a8xq0n4w7ms2t9r3vbk6e"),
        Row("td_01jc5a9zzzzzzzzzzzzzzzzzzz"),
        Row("td_01jc5b2kz8m3r6tq1w9xh4dn7y", done: true),
        Row("td_01jc5c90a2b7k4m8q1r6t3w9xz", removed: true),
    ];

    [Theory, Trait("Lane", "unit")]
    [InlineData("td_01jc5b", TodoCliVerb.Reopen, "td_01jc5b2kz8m3r6tq1w9xh4dn7y")]   // a done one
    [InlineData("td_01jc5c", TodoCliVerb.Restore, "td_01jc5c90a2b7k4m8q1r6t3w9xz")]   // a removed one
    [InlineData("td_01jc5a8", TodoCliVerb.Done, "td_01jc5a8xq0n4w7ms2t9r3vbk6e")]
    [InlineData("td_01jc5b", TodoCliVerb.Show, "td_01jc5b2kz8m3r6tq1w9xh4dn7y")]   // show looks at all
    [InlineData("td_01jc5c", TodoCliVerb.Show, "td_01jc5c90a2b7k4m8q1r6t3w9xz")]
    [InlineData("^td-01jc5a8", TodoCliVerb.Edit, "td_01jc5a8xq0n4w7ms2t9r3vbk6e")]   // the note's form
    [InlineData("td_01jc5a8xq0n4w7ms2t9r3vbk6e", TodoCliVerb.Remove, "td_01jc5a8xq0n4w7ms2t9r3vbk6e")]
    public void A_unique_prefix_among_the_verbs_todos_resolves(string typed, TodoCliVerb verb, string expected)
    {
        var id = TodoPrefix.Resolve(typed, verb, _rows);

        Assert.True(id.IsSuccess, id.IsError ? id.Error.ToString() : null);
        Assert.Equal(expected, id.Value.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("td_01jc5b", TodoCliVerb.Done)]   // that one is done, so done/edit/remove/keep don't see it
    [InlineData("td_01jc5a8", TodoCliVerb.Reopen)]   // that one is open
    [InlineData("td_01jc5a8", TodoCliVerb.Restore)]
    [InlineData("td_zzz", TodoCliVerb.Show)]
    public void A_prefix_no_todo_of_the_verb_has_is_SND_TODO_404(string typed, TodoCliVerb verb)
    {
        var id = TodoPrefix.Resolve(typed, verb, _rows);

        Assert.True(id.IsError);
        Assert.Equal("SND-TODO-404", id.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ambiguous_prefix_is_SND_TODO_010_naming_the_matches()
    {
        var id = TodoPrefix.Resolve("td_01jc5a", TodoCliVerb.Done, _rows);

        Assert.True(id.IsError);
        Assert.Equal("SND-TODO-010", id.Error.Code);
        Assert.Contains("td_01jc5a8xq0n4w7ms2t9r3vbk6e", id.Error.Message, StringComparison.Ordinal);
        Assert.Contains("td_01jc5a9zzzzzzzzzzzzzzzzzzz", id.Error.Message, StringComparison.Ordinal);
    }
}
