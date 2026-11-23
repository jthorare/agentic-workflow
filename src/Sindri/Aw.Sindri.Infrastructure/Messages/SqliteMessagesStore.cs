using Aw.Kernel;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>A stub until M1c (messages spec §14, §8.1): messages.db with its own migrations under Infrastructure/Messages/Migrations, FTS5 and retention.</summary>
internal sealed class SqliteMessagesStore : IMessagesStore
{
    public ValueTask<Result<IReadOnlyList<string>>> RebuildNeedsReply(string conversation, MessageTs thread, CancellationToken cancellationToken) =>
        throw new NotSupportedException("SqliteMessagesStore is not built yet: messages spec §14 phase M1c");
}
