using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Messages;
using Microsoft.Data.Sqlite;


namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>The retention pass and purge of messages.db (messages spec §8.1): what is kept, what goes, and the derived columns that go with it.</summary>
internal sealed partial class SqliteMessagesStore
{
    public async ValueTask<Result<IReadOnlyList<RetentionThread>>> RetentionThreads(MessageTs olderThan, CancellationToken cancellationToken)
    {
        if (!database.Exists)
        {
            return Array.Empty<RetentionThread>();
        }

        return await InTransaction<IReadOnlyList<RetentionThread>>(session => Translated(SqliteQueries.List(session,
            new SqlStatement(MessagesSql.SelectRetentionThreads, c => c.Bind("$cut", olderThan.Value), "retention threads"), RetentionThreadMapper.Instance, cancellationToken)), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<RetentionOutcome>> Expire(MessageTs olderThan, MessageTs capOlderThan, DateTimeOffset outboxOlderThan, IReadOnlyList<RetentionThread> keep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keep);
        if (!database.Exists)
        {
            return new RetentionOutcome(0, 0, 0);
        }

        return await InTransaction<RetentionOutcome>(session => ExpireIn(session, new ExpirePass(olderThan, capOlderThan, outboxOlderThan, keep), cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<int>> Purge(string? conversation, bool ownerState, CancellationToken cancellationToken)
    {
        if (!database.Exists)
        {
            return 0;
        }

        return await InTransaction<int>(async session =>
        {
            Action<SqliteCommand>? bind = conversation is null ? null : c => c.Bind("$c", conversation);
            string[] statements = conversation is null
                ? [MessagesSql.PurgeMessages, MessagesSql.PurgeDerived, MessagesSql.PurgeLandedOutbox, MessagesSql.PurgeUsers, MessagesSql.PurgeConversations,
                    .. ownerState ? new[] { MessagesSql.PurgeOwnerThreads, MessagesSql.PurgeDrafts, MessagesSql.PurgeOutbox } : []]
                : [MessagesSql.PurgeMessagesOf, MessagesSql.PurgeDerivedOf, MessagesSql.PurgeLandedOutboxOf,
                    .. ownerState ? new[] { MessagesSql.PurgeOwnerThreadsOf, MessagesSql.PurgeDraftsOf, MessagesSql.PurgeOutboxOf } : []];
            var messagesDeleted = 0;
            for (var i = 0; i < statements.Length; i++)
            {
                var run = await Execute(session, new SqlStatement(statements[i], bind, "purge"), cancellationToken).ConfigureAwait(false);
                if (run.IsError)
                {
                    return run.Error;
                }

                messagesDeleted = i == 0 ? run.Value : messagesDeleted;
            }

            return Results.Success(messagesDeleted);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<RetentionOutcome>> ExpireIn(ISqliteSession session, ExpirePass pass, CancellationToken cancellationToken)
    {
        var (olderThan, capOlderThan, outboxOlderThan, keep) = pass;
        var prepared = await PrepareKeep(session, keep, capOlderThan, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var affected = await Translated(SqliteQueries.List(session, new SqlStatement(MessagesSql.SelectExpiringThreads, c => c.Bind("$cut", olderThan.Value), "expiring threads"),
            ThreadMapper.Instance, cancellationToken)).ConfigureAwait(false);
        var deleted = affected.IsError ? affected.Error
            : await Execute(session, new SqlStatement(MessagesSql.DeleteExpired, c => c.Bind("$cut", olderThan.Value), "expired messages"), cancellationToken).ConfigureAwait(false);
        if (deleted.IsError)
        {
            return deleted.Error;
        }

        var expired = await SettleThreads(session, affected.Value, keep, cancellationToken).ConfigureAwait(false);
        var outbox = expired.IsError ? expired.Error
            : await Execute(session, new SqlStatement(MessagesSql.DeleteLandedOutbox, c => c.Bind("$at", outboxOlderThan), "landed outbox rows"), cancellationToken).ConfigureAwait(false);
        if (outbox.IsError)
        {
            return outbox.Error;
        }

        var dropped = await Execute(session, new SqlStatement(MessagesSql.DropKeep), cancellationToken).ConfigureAwait(false);
        return dropped.IsError ? dropped.Error : Results.Success(new RetentionOutcome(deleted.Value, outbox.Value, expired.Value));
    }

    // The kept set: each skipped thread's messages newer than the cap, plus its bounded context (20 messages around it).
    private static async ValueTask<Result<Unit>> PrepareKeep(ISqliteSession session, IReadOnlyList<RetentionThread> keep, MessageTs cap, CancellationToken cancellationToken)
    {
        foreach (var sql in new[] { MessagesSql.CreateKeep, MessagesSql.ClearKeep })
        {
            var made = await Execute(session, new SqlStatement(sql), cancellationToken).ConfigureAwait(false);
            if (made.IsError)
            {
                return made.Error;
            }
        }

        foreach (var thread in keep)
        {
            string[] statements = thread.Root.Length == 0 ? [MessagesSql.KeepDm] : [MessagesSql.KeepThread, MessagesSql.KeepContext];
            foreach (var sql in statements)
            {
                var kept = await Execute(session, new SqlStatement(sql, c => c.Bind("$c", thread.Conversation).Bind("$root", thread.Root).Bind("$cap", cap.Value), "kept thread"),
                    cancellationToken).ConfigureAwait(false);
                if (kept.IsError)
                {
                    return kept.Error;
                }
            }
        }

        return Results.Ok();
    }

    // After the delete: a thread with messages left is rebuilt; an empty one loses its derived columns, and a kept one (its skip ended at the cap) says "text expired".
    private async ValueTask<Result<int>> SettleThreads(ISqliteSession session, IReadOnlyList<ThreadRef> affected, IReadOnlyList<RetentionThread> keep, CancellationToken cancellationToken)
    {
        var expired = 0;
        var at = clock.GetUtcNow();
        foreach (var thread in affected)
        {
            var left = await Translated(SqliteQueries.Exists(session, new SqlStatement(MessagesSql.ThreadHasMessages, Bind(thread.Conversation, thread.Root), "thread"), cancellationToken)).ConfigureAwait(false);
            if (left.IsError)
            {
                return left.Error;
            }

            var wasKept = keep.Any(k => string.Equals(k.Conversation, thread.Conversation, StringComparison.Ordinal) && string.Equals(k.Root, thread.Root, StringComparison.Ordinal));
            var statement = left.Value
                ? new SqlStatement(MessagesSql.RebuildThread, Bind(thread.Conversation, thread.Root), "thread")
                : wasKept
                    ? new SqlStatement(MessagesSql.ExpireThreadText, c => Bind(thread.Conversation, thread.Root)(c.Bind("$at", at)), "thread")
                    : new SqlStatement(MessagesSql.ClearEmptyThread, Bind(thread.Conversation, thread.Root), "thread");
            var settled = await Execute(session, statement, cancellationToken).ConfigureAwait(false);
            if (settled.IsError)
            {
                return settled.Error;
            }

            expired += !left.Value && wasKept ? 1 : 0;
        }

        return expired;
    }

    /// <summary>One retention pass's cuts and the threads it skips.</summary>
    private sealed record ExpirePass(MessageTs OlderThan, MessageTs CapOlderThan, DateTimeOffset OutboxOlderThan, IReadOnlyList<RetentionThread> Keep);
}
