using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Host;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The todo ledger events (todo spec §4.1): each record round-trips through the package's JSON context, and the wire names are the spec's camelCase fields.</summary>
public sealed class TodoEventsTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly TodoId _id = TodoId.From("td_01jc5a8xq0n4w7ms2t9r3vbk6e");
    private static readonly string[] _links = ["sindri://reviews/acme/api/12", "sindri://linear/ENG-12"];

    [Fact, Trait("Lane", "unit")]
    public void Added_names_id_text_due_links_clientId_by_and_via() =>
        Check(new TodoAdded(_id, "rotate the staging key", new DateOnly(2026, 11, 10), _links, TodoClientId.From("c-1"), "human:cli", "pipe"),
            ["id", "text", "due", "links", "clientId", "by", "via"]);

    [Fact, Trait("Lane", "unit")]
    public void Due_is_a_plain_YYYY_MM_DD_date_never_a_time()
    {
        using var added = JsonDocument.Parse(Write(new TodoAdded(_id, "x", new DateOnly(2026, 11, 10), Links: [], ClientId: null, By: "human:ui")));
        using var changed = JsonDocument.Parse(Write(new TodoChanged(_id, 2, Text: null, new DateOnly(2026, 1, 2), ClearDue: false, Links: null, By: "human:ui")));
        using var side = JsonDocument.Parse(Write(new TodoSide("x", new DateOnly(2026, 11, 13), Links: [], Done: false, Removed: false)));

        Assert.Equal("2026-11-10", added.RootElement.GetProperty("due").GetString());
        Assert.Equal("2026-01-02", changed.RootElement.GetProperty("due").GetString());
        Assert.Equal("2026-11-13", side.RootElement.GetProperty("due").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void Added_without_via_leaves_the_via_field_out() =>
        Check(new TodoAdded(_id, "call the dentist", Due: null, Links: [], ClientId: null, By: "human:ui"), ["id", "text", "due", "links", "clientId", "by"]);

    [Fact, Trait("Lane", "unit")]
    public void Changed_names_the_changed_fields_and_the_due_clear_flag() =>
        Check(new TodoChanged(_id, 2, "rotate the prod key", Due: null, ClearDue: true, _links, "session:mcp", Via: null),
            ["id", "version", "text", "due", "clearDue", "links", "by"]);

    [Fact, Trait("Lane", "unit")]
    public void Changed_keeps_unchanged_apart_from_cleared_after_a_round_trip()
    {
        var back = Read<TodoChanged>(Write(new TodoChanged(_id, 3, Text: null, Due: null, ClearDue: true, Links: [], By: "human:ui")));

        Assert.True(back.ClearDue);
        Assert.Null(back.Due);
        Assert.Null(back.Text);
        Assert.Empty(back.Links!);  // why: set above, an empty list is not null
    }

    [Fact, Trait("Lane", "unit")]
    public void Done_reopened_removed_and_restored_name_id_version_by_and_via()
    {
        string[] names = ["id", "version", "by", "via"];
        Check(new TodoDone(_id, 2, "human:ui", "pipe"), names);
        Check(new TodoReopened(_id, 3, "human:ui", "pipe"), names);
        Check(new TodoRemoved(_id, 4, "human:ui", "pipe"), names);
        Check(new TodoRestored(_id, 5, "human:ui", "pipe"), names);
    }

    [Fact, Trait("Lane", "unit")]
    public void Conflicted_names_both_sides_and_the_note_hash()
    {
        var sindri = new TodoSide("reply to Sam about the offsite", new DateOnly(2026, 11, 13), _links, Done: true, Removed: false);
        var obsidian = new TodoSide(Text: null, Due: null, Links: [], Done: false, Removed: true);

        Check(new TodoConflicted(_id, sindri, obsidian, new string('a', 64)), ["id", "sindri", "obsidian", "noteHash"]);

        using var doc = JsonDocument.Parse(Write(new TodoSide("x", Due: null, Links: [], Done: false, Removed: true)));
        Assert.Equal("text,due,links,done,removed", string.Join(',', doc.RootElement.EnumerateObject().Select(p => p.Name)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Resolved_writes_the_kept_side_in_camel_case()
    {
        Check(new TodoConflictResolved(_id, TodoSideName.Obsidian, "- [ ] reply to Sam ^td-01jc5a8xq0n4w7ms2t9r3vbk6e", "human:ui"), ["id", "kept", "baseLine", "by"]);

        using var doc = JsonDocument.Parse(Write(new TodoConflictResolved(_id, TodoSideName.Sindri, "line", "human:ui")));
        Assert.Equal("sindri", doc.RootElement.GetProperty("kept").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void Reopened_conflict_names_id_by_and_via() =>
        Check(new TodoConflictReopened(_id, "session:mcp", "pipe"), ["id", "by", "via"]);

    [Fact, Trait("Lane", "unit")]
    public void The_nine_event_types_are_the_spec_names_and_are_in_the_catalog()
    {
        string[] types =
        [
            EventTypes.TodoAdded, EventTypes.TodoChanged, EventTypes.TodoDone, EventTypes.TodoReopened, EventTypes.TodoRemoved,
            EventTypes.TodoRestored, EventTypes.TodoConflicted, EventTypes.TodoConflictResolved, EventTypes.TodoConflictReopened,
        ];

        Assert.Equal(
            ["todo.added", "todo.changed", "todo.done", "todo.reopened", "todo.removed", "todo.restored", "todo.conflicted", "todo.conflict.resolved", "todo.conflict.reopened"],
            types);
        Assert.All(types, t => Assert.False(Assert.Single(EventCatalog.All, e => e.Type == t).Public));
    }

    private static byte[] Write<T>(T value) => AwJson.Serialize(value, _json);

    private static T Read<T>(byte[] bytes)
    {
        var read = AwJson.Deserialize<T>(bytes, _json);
        Assert.True(read.IsSuccess);
        return read.Value;
    }

    private static void Check<T>(T value, string[] names)
    {
        var bytes = Write(value);
        using var doc = JsonDocument.Parse(bytes);

        Assert.Equal(names.Order(StringComparer.Ordinal), doc.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(Encoding.UTF8.GetString(bytes), Encoding.UTF8.GetString(Write(Read<T>(bytes))));
    }
}
