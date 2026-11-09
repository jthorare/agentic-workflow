using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Tests.Todos.Note;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>REQ-W2: a secret typed into the note never reaches the ledger, in the events the pass appends for a settled conflict or an open hold either.</summary>
public sealed class NoteScrubSyncTests
{
    private const string Canary = "sk-canary-0123456789";
    private const string Mask = "[redacted]";

    private static ReplaceScrubber Scrubber => new(Canary, Mask);

    [Fact, Trait("Lane", "unit")]
    public async Task A_settled_conflict_does_not_carry_a_secret_from_the_note_line()
    {
        var first = TodoHarness.First;
        var rig = await SyncRig.Start(Scrubber);
        await rig.Harness.Seed(first, "reply to Sam");
        _ = await rig.Pass();
        await rig.Harness.Fold(EventTypes.TodoChanged, new TodoChanged(first, 2, "reply to Sam today " + Mask, Due: null, ClearDue: false, Links: null, "human:ui"));
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("reply to Sam", "reply to Sam later", StringComparison.Ordinal);
        _ = await rig.Pass();
        await rig.Harness.Fold(EventTypes.TodoConflicted, (await rig.Payloads<TodoConflicted>(EventTypes.TodoConflicted)).Single());
        rig.Files.Files[SyncRig.Path] = rig.Note!.Replace("later", "today " + Canary, StringComparison.Ordinal);

        _ = await rig.Pass();

        var settled = Assert.Single(await rig.Payloads<TodoConflictResolved>(EventTypes.TodoConflictResolved));
        Assert.DoesNotContain(Canary, settled.BaseLine, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_opened_hold_does_not_carry_a_secret_from_the_edited_note_lines()
    {
        var (rig, _) = await SyncRig.WithTodos(20);
        var lines = rig.Note!.Split('\n');
        for (var i = 2; i < 7; i++)
        {
            lines[i] += " " + Canary;
        }

        rig.Files.Files[SyncRig.Path] = string.Join('\n', lines);
        _ = await rig.Pass();

        var held = Assert.Single(await rig.Payloads<TodoNoteHeld>(EventTypes.TodoNoteHeld));
        Assert.NotEmpty(held.Changes);
        Assert.All(held.Changes, c => Assert.DoesNotContain(Canary, c.NoteLine ?? string.Empty, StringComparison.Ordinal));
    }
}
