using Aw.Sindri.Core.Messages;
using Aw.Sindri.Features.RetainMessages;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Messages;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Tests.Messages;

/// <summary>A temp Sindri home with a real messages.db (M1c, messages spec §8.1): the store, seed helpers and raw reads of the file. One per test.</summary>
internal sealed class MessagesHome : IDisposable
{
    private readonly TempHome _home = new();

    public MessagesHome(TimeProvider clock)
    {
        Database = new MessagesDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), clock);
        Store = new SqliteMessagesStore(Database, clock);
    }

    public string Home => _home.Path;

    public MessagesDatabase Database { get; }

    public SqliteMessagesStore Store { get; }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        Database.Dispose();
        _home.Dispose();
    }

    /// <summary>A ts at <paramref name="at"/>, with a non-zero fraction like Slack's.</summary>
    public static MessageTs Ts(DateTimeOffset at) => new(RetainMessagesService.TsOf(at).Value[..^6] + "000100");

    public async Task Conversation(string workspace, string id, ConversationKind kind) =>
        Assert.True((await Store.PutConversation(new StoredConversation(workspace, id, kind, id, ExtShared: false), Cancel)).IsSuccess);

    public async Task<IReadOnlyList<string>> Put(params StoredMessage[] messages)
    {
        var put = await Store.PutMessages(messages, Cancel);
        Assert.True(put.IsSuccess, put.IsError ? put.Error.Message : null);
        return put.Value;
    }

    /// <summary>One value of a read-only query on messages.db; NULL reads as null.</summary>
    public object? Scalar(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={Database.Path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    // why: every query passed here is a COUNT or a NOT NULL column, so the value is never NULL
    public long Count(string sql) => (long)Scalar(sql)!;

    /// <summary>Messages matching <paramref name="word"/> in the FTS index.</summary>
    public long Matches(string word) => Count($"SELECT COUNT(*) FROM messages_fts WHERE messages_fts MATCH '{word}'");

    public void Exec(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={Database.Path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = command.ExecuteNonQuery();
    }
}
