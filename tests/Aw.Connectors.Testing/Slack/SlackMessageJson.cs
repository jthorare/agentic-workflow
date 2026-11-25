using System.Text.Json.Nodes;
using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Testing.Slack;

/// <summary>A message as Slack's 'conversations.history' and 'conversations.replies' send it.</summary>
internal static class SlackMessageJson
{
    public static JsonObject Of(SlackMessage m, string user)
    {
        var o = new JsonObject { ["type"] = "message", ["ts"] = m.Ts, ["text"] = m.Text, ["user"] = user };
        if (m.Author is not null)
        {
            o["user_profile"] = new JsonObject { ["display_name"] = m.Author };
        }

        Optional(o, "thread_ts", m.ThreadTs);
        if (m.ReplyCount > 0)
        {
            o["reply_count"] = m.ReplyCount;
        }

        Optional(o, "latest_reply", m.LatestReply);
        Optional(o, "subtype", m.Subtype);
        return o;
    }

    private static void Optional(JsonObject o, string key, string? value)
    {
        if (value is not null)
        {
            o[key] = value;
        }
    }
}
