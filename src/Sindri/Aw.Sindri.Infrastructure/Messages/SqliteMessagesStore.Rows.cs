using Aw.Kernel.Data;
using Aw.Sindri.Core.Messages;
using Microsoft.Data.Sqlite;


namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>messages.db's row types and mappers.</summary>
internal sealed partial class SqliteMessagesStore
{
    /// <summary>True for a DM or group DM, whose row is the conversation.</summary>
    /// <param name="kind">The stored kind.</param>
    /// <returns>Whether it is a DM kind.</returns>
    internal static bool IsDmKind(string kind) => string.Equals(kind, "dm", StringComparison.Ordinal) || string.Equals(kind, "mpim", StringComparison.Ordinal);

    /// <summary>A conversation's workspace and whether its row is the conversation (a DM or group DM).</summary>
    private sealed record Place(string Workspace, string Conversation, bool IsDm);

    private sealed record ThreadRef(string Conversation, string Root);

    private sealed class PlaceMapper : IRowMapper<Place>
    {
        public static readonly PlaceMapper Instance = new();

        public Place Map(SqliteDataReader reader) =>
            new(reader.GetString(0), string.Empty, IsDmKind(reader.GetString(1)));
    }

    private sealed class ThreadMapper : IRowMapper<ThreadRef>
    {
        public static readonly ThreadMapper Instance = new();

        public ThreadRef Map(SqliteDataReader reader) => new(reader.GetString(0), reader.GetString(1));
    }

    private sealed class TextMapper : IRowMapper<string>
    {
        public static readonly TextMapper Instance = new();

        public string Map(SqliteDataReader reader) => reader.GetString(0);
    }

    private sealed class RetentionThreadMapper : IRowMapper<RetentionThread>
    {
        public static readonly RetentionThreadMapper Instance = new();

        public RetentionThread Map(SqliteDataReader reader)
        {
            var dm = IsDmKind(reader.GetString(1));
            var conversation = reader.GetString(2);
            var root = reader.GetString(3);
            return new RetentionThread(Key(reader.GetString(0), dm, conversation, root), conversation, root, reader.GetInt64(4) == 1, reader.GetInt64(5) == 1);
        }
    }
}
