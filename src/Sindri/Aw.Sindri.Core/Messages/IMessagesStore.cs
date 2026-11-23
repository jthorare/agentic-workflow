using Aw.Kernel;

namespace Aw.Sindri.Core.Messages;

/// <summary>The Messages store, messages.db (messages spec §8.1): a cache plus owner state, never a view of the ledger. M1c builds the SQLite adapter.</summary>
public interface IMessagesStore
{
    /// <summary>Recomputes Needs reply for one thread after its messages changed (W7): only that thread's row is touched.</summary>
    /// <param name="conversation">The Slack conversation.</param>
    /// <param name="thread">The thread root's ts; a DM passes its newest ts.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The keys of the rows the rebuild touched.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> RebuildNeedsReply(string conversation, MessageTs thread, CancellationToken cancellationToken);
}
