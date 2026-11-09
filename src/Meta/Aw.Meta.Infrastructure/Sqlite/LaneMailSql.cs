using Aw.Kernel.Data;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Microsoft.Data.Sqlite;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>The lane mail store's SQL.</summary>
internal static class LaneMailSql
{
    internal static SqlStatement Insert(string lane, string text, DateTimeOffset at) => new(
        "INSERT INTO lane_mail (lane, text, at) VALUES ($lane, $text, $at)",
        c => c.Bind("$lane", lane).Bind("$text", text).Bind("$at", at),
        "lane mail to " + lane);

    internal static readonly SqlStatement LastInsert = new("SELECT last_insert_rowid()", Subject: "lane mail number");

    internal static SqlStatement Owed(string lane) => new(
        "SELECT seq, lane, text, at, delivered_at FROM lane_mail WHERE lane = $lane AND settled_at IS NULL ORDER BY seq ASC",
        c => c.Bind("$lane", lane),
        "lane mail owed by " + lane);

    internal static SqlStatement Deliver(long seq, DateTimeOffset at) => new(
        "UPDATE lane_mail SET delivered_at = $at WHERE seq = $seq AND delivered_at IS NULL",
        c => c.Bind("$at", at).Bind("$seq", seq),
        "lane mail " + seq.ToString(System.Globalization.CultureInfo.InvariantCulture));

    internal static SqlStatement CountReply(string lane) => new(
        "INSERT INTO lane_replies (lane, n) VALUES ($lane, 1) ON CONFLICT(lane) DO UPDATE SET n = n + 1",
        c => c.Bind("$lane", lane),
        "lane reply counter " + lane);

    internal static SqlStatement ReplyNumber(string lane) => new(
        "SELECT n FROM lane_replies WHERE lane = $lane",
        c => c.Bind("$lane", lane),
        "lane reply counter " + lane);

    internal static SqlStatement Settle(string lane, DateTimeOffset at) => new(
        "UPDATE lane_mail SET settled_at = $at WHERE lane = $lane AND settled_at IS NULL AND delivered_at IS NOT NULL",
        c => c.Bind("$at", at).Bind("$lane", lane),
        "lane mail of " + lane);

    internal static SqlStatement SettleSeq(long seq, DateTimeOffset at) => new(
        "UPDATE lane_mail SET settled_at = $at, delivered_at = COALESCE(delivered_at, $at) WHERE seq = $seq AND settled_at IS NULL",
        c => c.Bind("$at", at).Bind("$seq", seq),
        "lane mail " + seq.ToString(System.Globalization.CultureInfo.InvariantCulture));

    internal static LaneMessage ReadOwed(SqliteDataReader r) =>
        new(r.GetInt64(0), r.GetString(1), r.GetString(2), SqliteRead.GetTimestamp(r, 3), Delivered: !r.IsDBNull(4));

    internal static SqlStatement InsertReplyMessage(InboxMessage m) => MessageSql.Insert(m);
}
