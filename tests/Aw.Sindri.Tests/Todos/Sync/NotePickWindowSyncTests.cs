using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>D11 and the hold: while a pick can still be undone, the sync leaves the lines the pick names as the note has them; when the window closes it goes on.</summary>
public sealed class NotePickWindowSyncTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_line_with_a_pick_in_its_window_is_left_alone_until_the_window_closes()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        await rig.Harness.Seed(TodoHarness.Second, "call the dentist");
        _ = await rig.Pass();
        var clock = rig.Harness.Slice.Clock;
        var prior = (await rig.Harness.Row(TodoHarness.First))!;
        rig.Harness.Picks.Remember(TodoHarness.First, new TodoPick(clock.GetUtcNow(), prior, prior.Version));
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("reply to Sam", "reply to Sam now", StringComparison.Ordinal).Replace("call the dentist", "call the dentist soon", StringComparison.Ordinal);

        _ = await rig.Pass();

        Assert.Equal([TodoHarness.Second], (await rig.Payloads<TodoChanged>(EventTypes.TodoChanged)).Select(c => c.Id));
        clock.Advance(TodoPickWindow.Span + TimeSpan.FromSeconds(1));

        _ = await rig.Pass();

        Assert.Equal([TodoHarness.Second, TodoHarness.First], (await rig.Payloads<TodoChanged>(EventTypes.TodoChanged)).Select(c => c.Id));
    }
}
