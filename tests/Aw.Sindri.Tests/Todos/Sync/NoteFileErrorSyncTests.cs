using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>A disk error in a pass is a failed pass in the status (doctor and the panel show why), never an exception out of the watch's call.</summary>
public sealed class NoteFileErrorSyncTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task An_IOException_from_a_file_call_fails_the_pass_with_its_message_and_counts_the_failure()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        rig.Files.WriteIntentThrows = new IOException("no space left on device");

        var first = await rig.Pass();
        var second = await rig.Pass();

        Assert.Equal((1, 2), (first.FailedInARow, second.FailedInARow));
        Assert.Contains("no space left on device", second.LastError, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_UnauthorizedAccessException_fails_the_pass_the_same_way()
    {
        var rig = await SyncRig.Start();
        await rig.Harness.Seed(TodoHarness.First, "reply to Sam");
        rig.Files.WriteIntentThrows = new UnauthorizedAccessException("denied");

        var status = await rig.Pass();

        Assert.Equal((1, "denied"), (status.FailedInARow, status.LastError));
    }
}
