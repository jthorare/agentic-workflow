using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Messages;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>
/// The messages.db adapter (messages spec §8.1): writes of conversations, messages and owner flags, the per-thread Needs reply rebuild (W7), the
/// retention pass's reads and deletes, and purge. Reads that find no file answer empty and never create it; writes create and migrate it.
/// </summary>
/// <param name="database">The file.</param>
/// <param name="clock">Stamps "text expired".</param>
internal sealed partial class SqliteMessagesStore(MessagesDatabase database, TimeProvider clock) : IMessagesStore
{
    private const string DmRoot = "";

    public ValueTask<Result<IReadOnlyList<string>>> RebuildNeedsReply(string conversation, MessageTs thread, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (!database.Exists)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<string>>>(Array.Empty<string>());
        }

        return InTransaction<IReadOnlyList<string>>(async session =>
        {
            var place = await FindPlace(session, conversation, cancellationToken).ConfigureAwait(false);
            if (place.IsError)
            {
                return place.Error;
            }

            var root = place.Value.IsDm ? DmRoot : thread.Value;
            var rebuilt = await Rebuild(session, place.Value, root, cancellationToken).ConfigureAwait(false);
            if (rebuilt.IsError)
            {
                return rebuilt.Error;
            }

            IReadOnlyList<string> keys = rebuilt.Value is { } key ? [key] : [];
            return Results.Success(keys);
        }, cancellationToken);
    }

    public ValueTask<Result<Unit>> PutConversation(StoredConversation conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return InTransaction<Unit>(async session =>
        {
            var written = await Execute(session, new SqlStatement(MessagesSql.UpsertConversation, c => c
                .Bind("$id", conversation.Conversation).Bind("$workspace", conversation.Workspace).Bind("$kind", KindText(conversation.Kind))
                .Bind("$name", conversation.Name).Bind("$ext", conversation.ExtShared), "conversation"), cancellationToken).ConfigureAwait(false);
            return written.IsError ? written.Error : Results.Ok();
        }, cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<string>>> PutMessages(IReadOnlyList<StoredMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return InTransaction<IReadOnlyList<string>>(async session =>
        {
            var places = new Dictionary<string, Place>(StringComparer.Ordinal);
            var threads = new List<(Place Place, string Root)>();
            foreach (var message in messages)
            {
                if (!places.TryGetValue(message.Conversation, out var place))
                {
                    var found = await FindPlace(session, message.Conversation, cancellationToken).ConfigureAwait(false);
                    if (found.IsError)
                    {
                        return found.Error;
                    }

                    place = found.Value;
                    places[message.Conversation] = place;
                }

                var root = place.IsDm ? DmRoot : (message.ThreadTs ?? message.Ts).Value;
                var written = await Execute(session, Insert(place, root, message), cancellationToken).ConfigureAwait(false);
                if (written.IsError)
                {
                    return written.Error;
                }

                if (!threads.Exists(t => string.Equals(t.Place.Conversation, place.Conversation, StringComparison.Ordinal) && string.Equals(t.Root, root, StringComparison.Ordinal)))
                {
                    threads.Add((place, root));
                }
            }

            return await RebuildAll(session, threads, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<string>>> RemoveMessage(string conversation, MessageTs ts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        if (!database.Exists)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<string>>>(Array.Empty<string>());
        }

        return InTransaction<IReadOnlyList<string>>(async session =>
        {
            var place = await FindPlace(session, conversation, cancellationToken).ConfigureAwait(false);
            if (place.IsError)
            {
                return place.Error;
            }

            var roots = await Translated(SqliteQueries.List(session, new SqlStatement(MessagesSql.SelectRootOf, c => c.Bind("$c", conversation).Bind("$ts", ts.Value), "message"),
                TextMapper.Instance, cancellationToken)).ConfigureAwait(false);
            if (roots.IsError || roots.Value.Count == 0)
            {
                return roots.IsError ? roots.Error : Array.Empty<string>();
            }

            var deleted = await Execute(session, new SqlStatement(MessagesSql.DeleteMessage, c => c.Bind("$c", conversation).Bind("$ts", ts.Value), "message"), cancellationToken).ConfigureAwait(false);
            return deleted.IsError ? deleted.Error : await RebuildAll(session, [(place.Value, roots.Value[0])], cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public ValueTask<Result<Unit>> PutThreadState(ThreadOwnerState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        return InTransaction<Unit>(async session =>
        {
            var place = await FindPlace(session, state.Conversation, cancellationToken).ConfigureAwait(false);
            var root = place.IsSuccess && place.Value.IsDm ? DmRoot : state.Thread?.Value ?? DmRoot;
            var written = await Execute(session, new SqlStatement(MessagesSql.UpsertOwnerState, c => c
                .Bind("$c", state.Conversation).Bind("$root", root).Bind("$later", state.Later).Bind("$follow", state.Follow), "thread state"), cancellationToken).ConfigureAwait(false);
            return written.IsError ? written.Error : Results.Ok();
        }, cancellationToken);
    }

    private static async ValueTask<Result<IReadOnlyList<string>>> RebuildAll(ISqliteSession session, IReadOnlyList<(Place Place, string Root)> threads, CancellationToken cancellationToken)
    {
        var touched = new List<string>(threads.Count);
        foreach (var (place, root) in threads)
        {
            var rebuilt = await Rebuild(session, place, root, cancellationToken).ConfigureAwait(false);
            if (rebuilt.IsError)
            {
                return rebuilt.Error;
            }

            if (rebuilt.Value is { } key)
            {
                touched.Add(key);
            }
        }

        return touched;
    }

    // One thread's row from its own messages; an emptied thread loses its derived columns. Returns the row's key when the row was written.
    private static async ValueTask<Result<string?>> Rebuild(ISqliteSession session, Place place, string root, CancellationToken cancellationToken)
    {
        var written = await Execute(session, new SqlStatement(MessagesSql.RebuildThread, Bind(place.Conversation, root), "thread"), cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        if (written.Value == 0)
        {
            written = await Execute(session, new SqlStatement(MessagesSql.ClearEmptyThread, Bind(place.Conversation, root), "thread"), cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }
        }

        return written.Value > 0 ? Key(place.Workspace, place.IsDm, place.Conversation, root) : null;
    }

    private static async ValueTask<Result<Place>> FindPlace(ISqliteSession session, string conversation, CancellationToken cancellationToken)
    {
        var found = await Translated(SqliteQueries.List(session, new SqlStatement(MessagesSql.SelectConversation, c => c.Bind("$c", conversation), "conversation"),
            PlaceMapper.Instance, cancellationToken)).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return found.Value.Count == 0
            ? SindriErrors.MessagesConversationUnknown.With("A message names a conversation messages.db has no row for; the sync writes the conversation first.")
            : found.Value[0] with { Conversation = conversation };
    }

    private static SqlStatement Insert(Place place, string root, StoredMessage message) => new(MessagesSql.UpsertMessage, c => c
        .Bind("$workspace", place.Workspace).Bind("$c", message.Conversation).Bind("$ts", message.Ts.Value).Bind("$threadTs", message.ThreadTs?.Value)
        .Bind("$root", root).Bind("$user", message.User).Bind("$text", message.Text).Bind("$blocks", message.Blocks).Bind("$reactions", message.Reactions)
        .Bind("$subtype", message.Subtype).Bind("$edited", message.EditedTs?.Value).Bind("$replies", message.ReplyCount)
        .Bind("$latestReply", message.LatestReplyTs?.Value).Bind("$partial", message.Partial).Bind("$files", message.Files)
        .Bind("$mine", message.Mine).Bind("$namesMe", message.NamesMe), "message");

    private static Action<SqliteCommand> Bind(string conversation, string root) => c => c.Bind("$c", conversation).Bind("$root", root);

    /// <summary>The row key (Home §3.1): 'slack:dm:&lt;workspace&gt;/&lt;conversation&gt;' for a DM or group DM, else 'slack:thread:&lt;workspace&gt;/&lt;conversation&gt;/&lt;root ts&gt;'.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="dm">True for a DM or group DM.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="root">The thread root's ts.</param>
    /// <returns>The key.</returns>
    internal static string Key(string workspace, bool dm, string conversation, string root) =>
        dm ? $"slack:dm:{workspace}/{conversation}" : $"slack:thread:{workspace}/{conversation}/{root}";

    internal static string KindText(ConversationKind kind) => kind switch
    {
        ConversationKind.Channel => "channel",
        ConversationKind.Private => "private",
        ConversationKind.Dm => "dm",
        ConversationKind.Mpim => "mpim",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a conversation kind"),
    };

    private static async ValueTask<Result<int>> Execute(ISqliteSession session, SqlStatement statement, CancellationToken cancellationToken) =>
        await Translated(SqliteQueries.Execute(session, statement, cancellationToken)).ConfigureAwait(false);

    private static async ValueTask<Result<T>> Translated<T>(ValueTask<Result<T>> pending)
    {
        var result = await pending.ConfigureAwait(false);
        return result.IsError ? MessagesDatabase.Translate(result.Error) : result;
    }

    private async ValueTask<Result<T>> InTransaction<T>(Func<ISqliteSession, ValueTask<Result<T>>> work, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return MessagesDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var result = await work(begun.Value).ConfigureAwait(false);
            if (result.IsError)
            {
                return result;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? MessagesDatabase.Translate(committed.Error) : result;
        }
    }
}
