using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Testing.Slack;

/// <summary>
/// The fake's 'search.messages' (messages spec §5.2: the to:me and from:me change detector and the cold-start fill). A query is words, each matched in the text
/// ignoring case, and the modifiers from:, to: and in: (me, @handle, &lt;@U…&gt;, a channel id or name). Every term must match. Hits come newest first, a page of
/// 'count' (20 by default) at a time by 'page'. Any other modifier fails the run (throws), so a reader that needs one extends the fake first. How Slack scores
/// to:me in a group DM is a guess until the probe's search line reads it: here a message by anyone else in a DM or group DM I am in is to me.
/// </summary>
internal static class FakeSlackSearch
{
    public static HttpResponseMessage Answer(FakeSlackServer server, Dictionary<string, string> query)
    {
        var text = query.GetValueOrDefault("query", string.Empty);
        var terms = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hits = server.Store.Everything()
            .Where(hit => terms.All(term => Matches(server, term, hit)))
            .OrderByDescending(static hit => SlackStore.Pad(hit.Message.Ts), StringComparer.Ordinal)
            .ToList();
        var count = Math.Clamp(Number(query, "count", 20), 1, 100);
        var page = Math.Max(1, Number(query, "page", 1));
        var matches = new JsonArray([.. hits.Skip((page - 1) * count).Take(count).Select(hit => Match(server, hit))]);
        return FakeSlackServer.Json(new JsonObject
        {
            ["ok"] = true,
            ["query"] = text,
            ["messages"] = new JsonObject
            {
                ["total"] = hits.Count,
                ["matches"] = matches,
                ["paging"] = new JsonObject { ["count"] = count, ["total"] = hits.Count, ["page"] = page, ["pages"] = (hits.Count + count - 1) / count },
            },
        });
    }

    private static bool Matches(FakeSlackServer server, string term, (string Channel, SlackMessage Message, string User) hit)
    {
        var colon = term.IndexOf(':', StringComparison.Ordinal);
        var key = colon > 0 ? term[..colon] : string.Empty;
        if (key.Length == 0 || !key.All(char.IsAsciiLetterLower) || term[(colon + 1)..].StartsWith("//", StringComparison.Ordinal))
        {
            return hit.Message.Text.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        var value = term[(colon + 1)..];
        return key switch
        {
            "from" => hit.User == UserId(server, value),
            "to" => IsTo(server, UserId(server, value), hit),
            "in" => IsIn(server, value, hit.Channel),
            _ => throw new InvalidOperationException($"The fake Slack does not know the search modifier {key}:."),
        };
    }

    /// <summary>The user id a from: or to: value names: me, @handle, a handle, &lt;@U…&gt; or a bare id.</summary>
    private static string UserId(FakeSlackServer server, string value)
    {
        var bare = value.Trim('<', '>').TrimStart('@');
        if (bare == "me")
        {
            return server.Identity.UserId;
        }

        return server.User(bare) is not null ? bare : server.Users.FirstOrDefault(u => u.Name == bare)?.Id ?? bare;
    }

    private static bool IsTo(FakeSlackServer server, string target, (string Channel, SlackMessage Message, string User) hit)
    {
        if (hit.User == target)
        {
            return false;
        }

        if (hit.Message.Text.Contains("<@" + target + ">", StringComparison.Ordinal))
        {
            return true;
        }

        return server.Conversation(hit.Channel) switch
        {
            { Kind: SlackConversationKind.Im } dm => target == server.Identity.UserId || target == dm.DmUser,
            { Kind: SlackConversationKind.Mpim } => target == server.Identity.UserId,
            _ => false,
        };
    }

    private static bool IsIn(FakeSlackServer server, string value, string channel)
    {
        var bare = value.Trim('<', '>').TrimStart('#').Split('|')[0];
        return bare == channel || server.Conversation(channel)?.Name == bare;
    }

    private static JsonObject Match(FakeSlackServer server, (string Channel, SlackMessage Message, string User) hit)
    {
        var conversation = server.Conversation(hit.Channel);
        var link = "https://" + server.Identity.Domain + ".slack.com/archives/" + hit.Channel + "/p" + hit.Message.Ts.Replace(".", string.Empty, StringComparison.Ordinal);
        if (hit.Message.ThreadTs is { } root && root != hit.Message.Ts)
        {
            link += "?thread_ts=" + root + "&cid=" + hit.Channel;
        }

        return new JsonObject
        {
            ["type"] = "message",
            ["ts"] = hit.Message.Ts,
            ["text"] = hit.Message.Text,
            ["user"] = hit.User,
            ["username"] = server.User(hit.User)?.Name ?? hit.User,
            ["channel"] = new JsonObject
            {
                ["id"] = hit.Channel,
                ["name"] = conversation?.Name ?? hit.Channel,
                ["is_private"] = conversation is not null && conversation.Kind is not SlackConversationKind.Public,
                ["is_im"] = conversation?.Kind is SlackConversationKind.Im,
                ["is_mpim"] = conversation?.Kind is SlackConversationKind.Mpim,
            },
            ["permalink"] = link,
        };
    }

    private static int Number(Dictionary<string, string> query, string key, int fallback)
        => query.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : fallback;
}
