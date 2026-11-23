using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Infrastructure.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1c (messages spec §8.1, W7): messages.db recomputes Needs reply per thread, never the whole list.</summary>
[Lane(TestLane.Integration)]
public sealed class MessagesStoreTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public Task Needs_reply_rebuild_touches_only_the_changed_thread() => RedUntil.Phase("M1c", async () =>
    {
        // M1c seeds two threads of one channel in a temp messages.db and changes the first; the second thread's row must not be rewritten
        var store = new SqliteMessagesStore();

        var touched = await store.RebuildNeedsReply("C0HARBOR", new MessageTs("1767771000.000100"), Cancel);

        Assert.True(touched.IsSuccess);
        Assert.Equal(["slack:thread:T0HARBOR/C0HARBOR/1767771000.000100"], touched.Value);
    });
}
