using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>ILaneMail over meta.db (lane_mail for the sends, lane_replies for reply numbers, messages for the replies themselves).</summary>
/// <param name="database">The meta database.</param>
internal sealed class SqliteLaneMail(MetaDatabase database) : ILaneMail
{
    public ValueTask<Result<long>> Send(string lane, string text, DateTimeOffset at, CancellationToken cancellationToken) =>
        database.Write<long>(
            async work =>
            {
                var inserted = await SqliteQueries.Execute(work, LaneMailSql.Insert(lane, text, at), cancellationToken).ConfigureAwait(false);
                if (inserted.IsError)
                {
                    return inserted.Error;
                }

                var number = await SqliteQueries.List(work, LaneMailSql.LastInsert, new RowMap<long>(static r => r.GetInt64(0)), cancellationToken).ConfigureAwait(false);
                return number.IsError ? number.Error : number.Value[0];
            },
            cancellationToken);

    public ValueTask<Result<IReadOnlyList<LaneMessage>>> Owed(string lane, CancellationToken cancellationToken) =>
        database.Query(LaneMailSql.Owed(lane), LaneMailSql.ReadOwed, cancellationToken);

    public ValueTask<Result<Unit>> Deliver(IReadOnlyList<long> seqs, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seqs);
        return database.Write(
            async work =>
            {
                foreach (var seq in seqs)
                {
                    var changed = await SqliteQueries.Execute(work, LaneMailSql.Deliver(seq, at), cancellationToken).ConfigureAwait(false);
                    if (changed.IsError)
                    {
                        return changed.Error;
                    }
                }

                return Results.Ok();
            },
            cancellationToken);
    }

    public ValueTask<Result<Unit>> Settle(IReadOnlyList<long> seqs, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seqs);
        return database.Write(
            async work =>
            {
                foreach (var seq in seqs)
                {
                    var changed = await SqliteQueries.Execute(work, LaneMailSql.SettleSeq(seq, at), cancellationToken).ConfigureAwait(false);
                    if (changed.IsError)
                    {
                        return changed.Error;
                    }
                }

                return Results.Ok();
            },
            cancellationToken);
    }

    public ValueTask<Result<MessageId>> Reply(LaneIdentity identity, string text, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return database.Write(
            async work =>
            {
                var counted = await SqliteQueries.Execute(work, LaneMailSql.CountReply(identity.Lane), cancellationToken).ConfigureAwait(false);
                if (counted.IsError)
                {
                    return counted.Error;
                }

                var number = await SqliteQueries.List(work, LaneMailSql.ReplyNumber(identity.Lane), new RowMap<long>(static r => r.GetInt64(0)), cancellationToken).ConfigureAwait(false);
                if (number.IsError)
                {
                    return number.Error;
                }

                var id = MessageId.From(string.Create(CultureInfo.InvariantCulture, $"lane:{identity.Lane}:{number.Value[0]}"));
                var message = new InboxMessage(id, identity.SessionName, identity.SessionName, text, at, MessageClassifier.Classify(text));
                var stored = await SqliteQueries.Execute(work, LaneMailSql.InsertReplyMessage(message), cancellationToken).ConfigureAwait(false);
                if (stored.IsError)
                {
                    return stored.Error;
                }

                var settled = await SqliteQueries.Execute(work, LaneMailSql.Settle(identity.Lane, at), cancellationToken).ConfigureAwait(false);
                return settled.IsError ? settled.Error : Results.Success(id);
            },
            cancellationToken);
    }
}
