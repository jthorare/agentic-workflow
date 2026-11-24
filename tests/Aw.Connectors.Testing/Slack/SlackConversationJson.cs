using System.Text.Json.Nodes;

namespace Aw.Connectors.Testing.Slack;

/// <summary>A conversation as Slack's 'users.conversations' lists it and 'conversations.info' describes it (the shapes the probe's answer set records).</summary>
internal static class SlackConversationJson
{
    public static JsonObject Of(SlackConversationSeed c, bool info)
    {
        var o = new JsonObject
        {
            ["id"] = c.Id,
            ["is_channel"] = c.Kind is SlackConversationKind.Public,
            ["is_group"] = c.Kind is SlackConversationKind.Private,
            ["is_im"] = c.Kind is SlackConversationKind.Im,
            ["is_mpim"] = c.Kind is SlackConversationKind.Mpim,
            ["is_private"] = c.Kind is not SlackConversationKind.Public,
            ["is_archived"] = c.IsArchived,
            ["is_ext_shared"] = c.IsExtShared,
        };
        if (c.Kind is SlackConversationKind.Im)
        {
            o["user"] = c.DmUser;
        }
        else
        {
            o["name"] = c.Name;
            o["is_member"] = c.IsMember;
        }

        if (c.Topic.Length > 0)
        {
            o["topic"] = new JsonObject { ["value"] = c.Topic };
        }

        return info ? WithReadState(o, c) : o;
    }

    private static JsonObject WithReadState(JsonObject o, SlackConversationSeed c)
    {
        if (c.LastRead is not null)
        {
            o["last_read"] = c.LastRead;
        }

        if (c.UnreadCount is { } unread)
        {
            o["unread_count"] = unread;
            o["unread_count_display"] = unread;
        }

        return o;
    }
}
