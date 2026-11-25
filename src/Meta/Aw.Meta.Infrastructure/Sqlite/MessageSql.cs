using Aw.Kernel.Data;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Microsoft.Data.Sqlite;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>The message store's SQL: named constants and the statements that bind them.</summary>
internal static class MessageSql
{
    internal const string InsertMessage =
        "INSERT OR IGNORE INTO messages (id, from_socket, from_name, body, at, kind, pr, lane) " +
        "VALUES ($id, $from_socket, $from_name, $body, $at, $kind, $pr, $lane)";

    internal static readonly SqlStatement All = new(
        "SELECT id, from_socket, from_name, body, at, kind, pr, lane, resolution, resolution_reason, resolution_until, shown_at " +
        "FROM messages ORDER BY at ASC, seq ASC",
        Subject: "messages");

    internal static readonly SqlStatement AllOutbound = new(
        "SELECT to_peer, message, at, msg_id FROM outbound ORDER BY at ASC, seq ASC",
        Subject: "outbound");

    internal static readonly SqlStatement SelectRole = new("SELECT session_id, name FROM role WHERE singleton = 1", Subject: "role");

    internal static readonly SqlStatement AllTranscripts = new("SELECT transcript FROM offsets ORDER BY transcript ASC", Subject: "offsets");

    internal static SqlStatement Offset(string transcript) =>
        new("SELECT byte_offset FROM offsets WHERE transcript = $transcript", c => c.Bind("$transcript", transcript), "offset " + transcript);

    internal static SqlStatement SetOffset(string transcript, long offset) =>
        new(
            "INSERT INTO offsets (transcript, byte_offset) VALUES ($transcript, $offset) " +
            "ON CONFLICT(transcript) DO UPDATE SET byte_offset = excluded.byte_offset",
            c => c.Bind("$transcript", transcript).Bind("$offset", offset),
            "offset " + transcript);

    internal static SqlStatement Insert(InboxMessage m) => new(
        InsertMessage,
        c => c.Bind("$id", m.Id.Value).Bind("$from_socket", m.From).Bind("$from_name", m.FromName).Bind("$body", m.Body)
            .Bind("$at", m.At).Bind("$kind", m.Class.Kind.ToString()).Bind("$pr", m.Class.Pr).Bind("$lane", m.Class.Lane),
        "message " + m.Id.Value);

    internal static SqlStatement Resolve(MessageId id, MessageResolution resolution) => new(
        "UPDATE messages SET resolution = $resolution, resolution_reason = $reason, resolution_until = $until WHERE id = $id",
        c => c.Bind("$resolution", resolution.Kind.ToString()).Bind("$reason", resolution.Reason).BindTime("$until", resolution.Until).Bind("$id", id.Value),
        "message " + id.Value);

    internal static SqlStatement MarkShown(MessageId id, DateTimeOffset at) => new(
        "UPDATE messages SET shown_at = $at WHERE id = $id",
        c => c.Bind("$at", at).Bind("$id", id.Value),
        "message " + id.Value);

    internal static SqlStatement InsertOutbound(Outbound sent) => new(
        "INSERT INTO outbound (msg_id, to_peer, message, at) VALUES ($msg_id, $to, $message, $at)",
        c => c.Bind("$msg_id", sent.Id?.Value).Bind("$to", sent.To).Bind("$message", sent.Message).Bind("$at", sent.At),
        "outbound to " + sent.To);

    internal static SqlStatement SetRole(MetaRole role) => new(
        "INSERT INTO role (singleton, session_id, name) VALUES (1, $session, $name) " +
        "ON CONFLICT(singleton) DO UPDATE SET session_id = excluded.session_id, name = excluded.name",
        c => c.Bind("$session", role.Session.Value).Bind("$name", role.Name),
        "role");

    internal static InboxMessage ReadMessage(SqliteDataReader r)
    {
        var classification = new Classification(
            Enum.Parse<MessageKind>(r.GetString(5)),
            r.IsDBNull(6) ? null : checked((int)r.GetInt64(6)),
            r.IsDBNull(7) ? null : r.GetString(7));
        var resolution = r.IsDBNull(8)
            ? null
            : new MessageResolution(Enum.Parse<MessageResolutionKind>(r.GetString(8)), r.GetString(9), r.ReadTime(10));
        return new InboxMessage(MessageId.From(r.GetString(0)), r.GetString(1), r.GetString(2), r.GetString(3), SqliteRead.GetTimestamp(r, 4), classification)
        {
            Resolution = resolution,
            ShownAt = r.ReadTime(11),
        };
    }

    internal static Outbound ReadOutbound(SqliteDataReader r) =>
        new(r.GetString(0), r.GetString(1), SqliteRead.GetTimestamp(r, 2))
        {
            Id = r.IsDBNull(3) ? null : MessageId.From(r.GetString(3)),
        };
}
