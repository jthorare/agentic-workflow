using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>IMessageStore over meta.db (messages, outbound, offsets and role tables).</summary>
/// <param name="database">The meta database.</param>
internal sealed class SqliteMessageStore(MetaDatabase database) : IMessageStore
{
    public async ValueTask<Result<long>> Offset(string transcript, CancellationToken cancellationToken)
    {
        var rows = await database.Query(MessageSql.Offset(transcript), reader => reader.GetInt64(0), cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : rows.Value.Count == 0 ? 0L : rows.Value[0];
    }

    public ValueTask<Result<IReadOnlyList<MessageId>>> RecordInbound(string transcript, long offset, IReadOnlyList<InboxMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return database.Write<IReadOnlyList<MessageId>>(
            async work =>
            {
                var added = new List<MessageId>();
                foreach (var message in messages)
                {
                    var changed = await SqliteQueries.Execute(work, MessageSql.Insert(message), cancellationToken).ConfigureAwait(false);
                    if (changed.IsError)
                    {
                        return changed.Error;
                    }

                    if (changed.Value > 0)
                    {
                        added.Add(message.Id);
                    }
                }

                var moved = await SqliteQueries.Execute(work, MessageSql.SetOffset(transcript, offset), cancellationToken).ConfigureAwait(false);
                return moved.IsError ? moved.Error : added;
            },
            cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<InboxMessage>>> Messages(CancellationToken cancellationToken) =>
        database.Query(MessageSql.All, MessageSql.ReadMessage, cancellationToken);

    public ValueTask<Result<Unit>> Resolve(MessageId id, MessageResolution resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return database.Write(
            async work =>
            {
                var changed = await SqliteQueries.Execute(work, MessageSql.Resolve(id, resolution), cancellationToken).ConfigureAwait(false);
                return changed.IsError ? changed.Error
                    : changed.Value == 0 ? MetaErrors.MessageNotFound.With($"no message {id.Value}")
                    : Results.Ok();
            },
            cancellationToken);
    }

    public ValueTask<Result<Unit>> MarkShown(IReadOnlyList<MessageId> ids, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return database.Write(
            async work =>
            {
                foreach (var id in ids)
                {
                    var changed = await SqliteQueries.Execute(work, MessageSql.MarkShown(id, at), cancellationToken).ConfigureAwait(false);
                    if (changed.IsError)
                    {
                        return changed.Error;
                    }
                }

                return Results.Ok();
            },
            cancellationToken);
    }

    public ValueTask<Result<Unit>> RecordOutbound(Outbound sent, CancellationToken cancellationToken) =>
        database.Run(MessageSql.InsertOutbound(sent), cancellationToken);

    public ValueTask<Result<IReadOnlyList<Outbound>>> Sent(CancellationToken cancellationToken) =>
        database.Query(MessageSql.AllOutbound, MessageSql.ReadOutbound, cancellationToken);

    public async ValueTask<Result<MetaRole?>> Role(CancellationToken cancellationToken)
    {
        var rows = await database.Query(
            MessageSql.SelectRole,
            reader => new MetaRole(SessionId.From(reader.GetString(0)), reader.GetString(1)),
            cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    public ValueTask<Result<Unit>> SetRole(MetaRole role, CancellationToken cancellationToken) =>
        database.Run(MessageSql.SetRole(role), cancellationToken);
}
