using Aw.Sindri.Core.Messages;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Messages;

/// <summary>
/// The Messages read routes' seed (M1f, messages spec §8.2): one workspace with the channel #harbor and Sam's DM, written through the host's own
/// <see cref="IMessagesStore"/> the way the sync writes them. The users, a star and a gap have no store write yet (the sync, M1e, brings them), so they go
/// straight into messages.db.
/// </summary>
internal static class MessageSeed
{
    public const string Workspace = "T0HARBOR";
    public const string Harbor = "C0HARBOR";
    public const string Sam = "D0SAM";
    public const string AdaRoot = "1767771000.000100";
    public const string KimReply = "1767771500.000100";
    public const string SamAsk = "1767771200.000100";
    public const string LeeLunch = "1767772000.000100";
    public const string HarborKey = "slack:thread:T0HARBOR/C0HARBOR/1767771000.000100";
    public const string SamKey = "slack:dm:T0HARBOR/D0SAM";
    public const string LunchKey = "slack:thread:T0HARBOR/C0HARBOR/1767772000.000100";

    /// <summary>
    /// Writes #harbor (Ada's thread naming me with Kim's reply, and Lee's lunch line), Sam's DM ask, Sam's user row, a star on Sam's DM and a gap of 12
    /// between Ada's thread and the lunch line.
    /// </summary>
    public static async Task Seed(IServiceProvider services, string home, CancellationToken ct)
    {
        var store = services.GetRequiredService<IMessagesStore>();
        Assert.True((await store.PutConversation(new StoredConversation(Workspace, Harbor, ConversationKind.Channel, "harbor", ExtShared: false), ct)).IsSuccess);
        Assert.True((await store.PutConversation(new StoredConversation(Workspace, Sam, ConversationKind.Dm, "U0SAM", ExtShared: false), ct)).IsSuccess);
        StoredMessage[] messages =
        [
            new StoredMessage(Harbor, new MessageTs(AdaRoot), new MessageTs(AdaRoot), "U0ADA", "<@U0JOI> review the fare table PR?", Mine: false, NamesMe: true)
            {
                ReplyCount = 1,
                LatestReplyTs = new MessageTs(KimReply),
            },
            new StoredMessage(Harbor, new MessageTs(KimReply), new MessageTs(AdaRoot), "U0KIM", "and the group path too", Mine: false, NamesMe: false),
            new StoredMessage(Harbor, new MessageTs(LeeLunch), null, "U0LEE", "lunch Friday", Mine: false, NamesMe: false),
            new StoredMessage(Sam, new MessageTs(SamAsk), null, "U0SAM", "can you ship the fare table today?", Mine: false, NamesMe: false),
        ];
        var put = await store.PutMessages(messages, ct);
        Assert.True(put.IsSuccess, put.IsError ? put.Error.Message : null);

        Exec(home,
            "INSERT INTO users (id, workspace, name, real_name, display_name) VALUES ('U0SAM', 'T0HARBOR', 'sam', 'Sam Lee', 'Sam Lee')",
            "UPDATE conversations SET starred = 1 WHERE id = 'D0SAM'",
            $"UPDATE conversations SET gaps = '[{{\"after\":\"{AdaRoot}\",\"before\":\"{LeeLunch}\",\"missing\":12}}]' WHERE id = 'C0HARBOR'");
    }

    /// <summary>Runs statements on the home's messages.db, outside the store (the writes the store has no method for yet).</summary>
    public static void Exec(string home, params string[] statements)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(home, "messages.db")};Pooling=False");
        connection.Open();
        foreach (var sql in statements)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            _ = command.ExecuteNonQuery();
        }
    }
}
