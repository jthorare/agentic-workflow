using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Messages;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>The reads behind the Messages routes (messages spec §8.2): lists, conversations, a window and a thread, each in one read-only snapshot.</summary>
internal sealed partial class SqliteMessagesStore
{
    public async ValueTask<Result<IReadOnlyList<ThreadRow>>> Rows(MessageList list, CancellationToken cancellationToken)
    {
        if (!database.Exists)
        {
            return Array.Empty<ThreadRow>();
        }

        var sql = list switch
        {
            MessageList.NeedsReply => MessagesReadSql.NeedsReplyRows,
            MessageList.Unread => MessagesReadSql.UnreadRows,
            MessageList.Later => MessagesReadSql.LaterRows,
            MessageList.Drafts => MessagesReadSql.DraftRows,
            _ => throw new ArgumentOutOfRangeException(nameof(list), list, "not a list"),
        };
        return await InRead(session => Translated(SqliteQueries.List(session, new SqlStatement(sql, Bind: null, "list rows"), ThreadRowMapper.Instance, cancellationToken)), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyList<ConversationSummary>>> Conversations(CancellationToken cancellationToken)
    {
        if (!database.Exists)
        {
            return Array.Empty<ConversationSummary>();
        }

        return await InRead(session => Translated(SqliteQueries.List(session, new SqlStatement(MessagesReadSql.Conversations, Bind: null, "conversations"), ConversationMapper.Instance, cancellationToken)), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<ConversationWindow>> Window(string conversation, MessageTs? before, MessageTs? after, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (!database.Exists)
        {
            return Unknown(conversation);
        }

        return await InRead<ConversationWindow>(async session =>
        {
            var known = await Known(session, conversation, cancellationToken).ConfigureAwait(false);
            if (known.IsError)
            {
                return known.Error;
            }

            var statement = after is { } newer
                ? new SqlStatement(MessagesReadSql.WindowAfter, c => c.Bind("$c", conversation).Bind("$after", newer.Value).Bind("$limit", limit), "window")
                : before is { } older
                ? new SqlStatement(MessagesReadSql.WindowBefore, c => c.Bind("$c", conversation).Bind("$before", older.Value).Bind("$limit", limit), "window")
                : new SqlStatement(MessagesReadSql.WindowNewest, c => c.Bind("$c", conversation).Bind("$limit", limit), "window");
            var lines = await Translated(SqliteQueries.List(session, statement, LineMapper.Instance, cancellationToken)).ConfigureAwait(false);
            if (lines.IsError || lines.Value.Count == 0)
            {
                return lines.IsError ? lines.Error : new ConversationWindow([], []);
            }

            var gaps = await Translated(SqliteQueries.List(session, new SqlStatement(MessagesReadSql.WindowGaps, c => c
                .Bind("$c", conversation).Bind("$first", lines.Value[0].Ts.Value).Bind("$last", lines.Value[^1].Ts.Value), "gaps"), new GapMapper(conversation), cancellationToken)).ConfigureAwait(false);
            return gaps.IsError ? gaps.Error : new ConversationWindow(lines.Value, gaps.Value);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<StoredThread>> Thread(string conversation, MessageTs thread, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (!database.Exists)
        {
            return Unknown(conversation);
        }

        return await InRead<StoredThread>(async session =>
        {
            var place = await FindPlace(session, conversation, cancellationToken).ConfigureAwait(false);
            if (place.IsError)
            {
                return string.Equals(place.Error.Code, SindriErrors.MessagesConversationUnknown.Code, StringComparison.Ordinal) ? Unknown(conversation) : place.Error;
            }

            var lines = await Translated(SqliteQueries.List(session, new SqlStatement(MessagesReadSql.ThreadLines, c => c.Bind("$c", conversation).Bind("$ts", thread.Value), "thread"),
                LineMapper.Instance, cancellationToken)).ConfigureAwait(false);
            if (lines.IsError)
            {
                return lines.Error;
            }

            if (lines.Value.FirstOrDefault(l => string.Equals(l.Ts.Value, thread.Value, StringComparison.Ordinal)) is not { } root)
            {
                return SindriErrors.MessagesConversationUnknown.With($"messages.db holds no thread {thread.Value} in {conversation}; open the conversation once to load it.");
            }

            var rootTs = place.Value.IsDm ? DmRoot : thread.Value;
            var rows = await Translated(SqliteQueries.List(session, new SqlStatement(MessagesReadSql.RowOfThread, Bind(conversation, rootTs), "thread row"), ThreadRowMapper.Instance, cancellationToken)).ConfigureAwait(false);
            return rows.IsError ? rows.Error : new StoredThread(root, [.. lines.Value.Where(l => !ReferenceEquals(l, root))], rows.Value.Count == 0 ? null : rows.Value[0]);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static Error Unknown(string conversation) =>
        SindriErrors.MessagesConversationUnknown.With($"messages.db holds no conversation {conversation}; open it once to load it, or run `sindri messages status`.");

    private static async ValueTask<Result<Unit>> Known(ISqliteSession session, string conversation, CancellationToken cancellationToken)
    {
        var exists = await Translated(SqliteQueries.Exists(session, new SqlStatement(MessagesReadSql.ConversationExists, c => c.Bind("$c", conversation), "conversation"), cancellationToken)).ConfigureAwait(false);
        return exists.IsError ? exists.Error : exists.Value ? Results.Ok() : Unknown(conversation);
    }

    private async ValueTask<Result<T>> InRead<T>(Func<ISqliteSession, ValueTask<Result<T>>> work, CancellationToken cancellationToken)
    {
        var begun = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return MessagesDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            return await work(begun.Value).ConfigureAwait(false);
        }
    }

    internal static ConversationKind KindOf(string kind) => kind switch
    {
        "channel" => ConversationKind.Channel,
        "private" => ConversationKind.Private,
        "dm" => ConversationKind.Dm,
        "mpim" => ConversationKind.Mpim,
        _ => throw new InvalidOperationException($"messages.db holds an unknown conversation kind '{kind}'"),
    };

    private sealed class ThreadRowMapper : IRowMapper<ThreadRow>
    {
        public static readonly ThreadRowMapper Instance = new();

        public ThreadRow Map(SqliteDataReader reader)
        {
            var kind = reader.GetString(1);
            var dm = IsDmKind(kind);
            var conversation = reader.GetString(2);
            var root = reader.GetString(3);
            var newestIsMine = reader.GetInt64(8) == 1;
            var key = dm ? ThreadRow.DmKey(reader.GetString(0), conversation) : ThreadRow.ThreadKey(reader.GetString(0), conversation, new MessageTs(root));   // the same text the store's Key writes
            return new ThreadRow(key, KindOf(kind), conversation, new MessageTs(reader.GetString(4)))
            {
                Thread = dm ? null : new MessageTs(root),
                Mine = Reason(dm, wrote: reader.GetInt64(5) == 1, namedMe: reader.GetInt64(6) == 1, followed: reader.GetInt64(7) == 1),
                NewestIsMine = newestIsMine,
                RepliedAfterNewest = newestIsMine,
                NamesMe = reader.GetInt64(9) == 1,
                NewestText = reader.GetString(10),
            };
        }

        private static MineReason? Reason(bool dm, bool wrote, bool namedMe, bool followed) =>
            dm ? MineReason.DirectMessage : wrote ? MineReason.WroteInThread : namedMe ? MineReason.NamedMe : followed ? MineReason.Followed : null;
    }

    private sealed class ConversationMapper : IRowMapper<ConversationSummary>
    {
        public static readonly ConversationMapper Instance = new();

        public ConversationSummary Map(SqliteDataReader reader) =>
            new(reader.GetString(0), KindOf(reader.GetString(1)), reader.GetString(2), reader.GetInt64(3) == 1, reader.GetInt32(4));
    }

    private sealed class LineMapper : IRowMapper<MessageLine>
    {
        public static readonly LineMapper Instance = new();

        public MessageLine Map(SqliteDataReader reader) => new(new MessageTs(reader.GetString(0)), reader.GetString(1), reader.GetString(2))
        {
            Thread = reader.IsDBNull(3) ? null : new MessageTs(reader.GetString(3)),
            Replies = reader.GetInt32(4),
            Partial = reader.GetInt64(5) == 1,
            AuthorName = reader.IsDBNull(6) ? null : reader.GetString(6),
        };
    }

    private sealed class GapMapper(string conversation) : IRowMapper<GapMarker>
    {
        public GapMarker Map(SqliteDataReader reader) =>
            new(conversation, new MessageTs(reader.GetString(0)), new MessageTs(reader.GetString(1)), reader.GetInt32(2));
    }
}
