using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Testing.Slack;

/// <summary>
/// The made-up workspace the fake Slack serves for the Messages page (messages spec §8.1, §11: fixtures are made up): me, three people and a bot; a DM whose
/// newest message waits on me; a group DM; a public channel with a mention of me and a thread I answered that has a newer reply; a private channel; one
/// custom emoji and its alias. The constants are the answer key a test or the fixture script checks against.
/// </summary>
public static class SlackWorkspaceSeed
{
    /// <summary>My user id (the token's user, <see cref="FakeSlackServer.Identity"/>).</summary>
    public const string Me = "U0000ME";

    /// <summary>The person I have a DM with.</summary>
    public const string Pat = "U0001PAT";

    /// <summary>The person who mentions me in the public channel.</summary>
    public const string Lee = "U0002LEE";

    /// <summary>The person who starts the thread.</summary>
    public const string Kim = "U0003KIM";

    /// <summary>A bot user.</summary>
    public const string Bot = "U0004BOT";

    /// <summary>The DM with <see cref="Pat"/>.</summary>
    public const string DirectMessage = "D0001PAT";

    /// <summary>The group DM with <see cref="Pat"/> and <see cref="Lee"/>.</summary>
    public const string GroupMessage = "G0001MPD";

    /// <summary>The public channel.</summary>
    public const string General = "C0001GEN";

    /// <summary>The private channel.</summary>
    public const string Private = "C0002OPS";

    /// <summary>The DM message I answered.</summary>
    public const string DmAnsweredTs = "1763625600.000100";

    /// <summary>My answer in the DM, the DM's last_read.</summary>
    public const string DmMyAnswerTs = "1763625900.000100";

    /// <summary>The DM's newest message, from <see cref="Pat"/>: it waits on me.</summary>
    public const string DmWaitingTs = "1763712000.000100";

    /// <summary>The thread root in <see cref="General"/>, from <see cref="Kim"/>.</summary>
    public const string ThreadRootTs = "1763704800.000100";

    /// <summary>My reply in the thread.</summary>
    public const string ThreadMyReplyTs = "1763705100.000100";

    /// <summary>The thread's newest reply, from <see cref="Kim"/>, after mine.</summary>
    public const string ThreadNewReplyTs = "1763709000.000100";

    /// <summary>The message in <see cref="General"/> that mentions me, from <see cref="Lee"/>.</summary>
    public const string MentionTs = "1763708400.000100";

    /// <summary>The group DM's message, from <see cref="Pat"/>.</summary>
    public const string GroupTs = "1763710000.000100";

    /// <summary>The private channel's message, from <see cref="Lee"/>.</summary>
    public const string PrivateTs = "1763690000.000100";

    /// <summary>Seeds <paramref name="server"/> with the workspace.</summary>
    /// <param name="server">The fake.</param>
    /// <returns>The fake.</returns>
    public static FakeSlackServer Apply(FakeSlackServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        server.Identity = new SlackIdentity("T0000FIX", "Fixture Workspace", "fixture", Me, "fixture-me");
        People(server);
        Conversations(server);
        Messages(server);
        return server
            .AddEmoji("fixture-ok", "https://emoji.slack-edge.com/T0000FIX/fixture-ok/0a1b2c3d.png")
            .AddEmoji("fixture-yes", "alias:fixture-ok");
    }

    private static void People(FakeSlackServer server) => _ = server
        .AddUser(new SlackUserSeed(Me, "fixture-me", "Fixture Me") { RealName = "Fixture Me" })
        .AddUser(new SlackUserSeed(Pat, "pat", "Pat Fixture") { RealName = "Pat Fixture", Title = "Release lead" })
        .AddUser(new SlackUserSeed(Lee, "lee", "Lee Sample") { RealName = "Lee Sample", TimeZone = "America/New_York" })
        .AddUser(new SlackUserSeed(Kim, "kim", "Kim Demo") { RealName = "Kim Demo", StatusText = "Heads down" })
        .AddUser(new SlackUserSeed(Bot, "buildbot", "Build Bot") { IsBot = true });

    private static void Conversations(FakeSlackServer server) => _ = server
        .AddConversation(new SlackConversationSeed(DirectMessage, SlackConversationKind.Im, string.Empty) { DmUser = Pat, LastRead = DmMyAnswerTs, UnreadCount = 1 })
        .AddConversation(new SlackConversationSeed(GroupMessage, SlackConversationKind.Mpim, "mpdm-fixture-me--pat--lee-1") { LastRead = GroupTs, UnreadCount = 0 })
        .AddConversation(new SlackConversationSeed(General, SlackConversationKind.Public, "general") { Topic = "Builds and releases", LastRead = ThreadMyReplyTs })
        .AddConversation(new SlackConversationSeed(Private, SlackConversationKind.Private, "ops-rota") { LastRead = PrivateTs });

    private static void Messages(FakeSlackServer server) => _ = server
        .Add(DirectMessage, Message(DmAnsweredTs, "Pat Fixture", "Morning, is the staging box free today?"), Pat)
        .Add(DirectMessage, Message(DmMyAnswerTs, "Fixture Me", "Yes, it is all yours."), Me)
        .Add(DirectMessage, Message(DmWaitingTs, "Pat Fixture", "Can you look at the release notes before lunch?"), Pat)
        .Add(GroupMessage, Message(GroupTs, "Pat Fixture", "Lunch at noon on Friday?"), Pat)
        .Add(General, Message(ThreadRootTs, "Kim Demo", "Release checklist for Friday: who takes the changelog?"), Kim)
        .Add(General, Message(ThreadMyReplyTs, "Fixture Me", "I can take the changelog.", ThreadRootTs), Me)
        .Add(General, Message(ThreadNewReplyTs, "Kim Demo", "Thanks! Can you post the summary here when it is done?", ThreadRootTs), Kim)
        .Add(General, Message(MentionTs, "Lee Sample", "<@" + Me + "> the nightly build is red on main, can you check?"), Lee)
        .Add(Private, Message(PrivateTs, "Lee Sample", "The rota swap for next week is confirmed."), Lee);

    private static SlackMessage Message(string ts, string author, string text, string? threadTs = null) => new(ts, threadTs, author, text, 0, null);
}
