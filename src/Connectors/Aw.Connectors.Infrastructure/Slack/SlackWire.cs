using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Infrastructure.Slack;

/// <summary>Reads Slack's replies: headers, the error slug of an ok:false answer and the messages of a history or replies page.</summary>
internal static partial class SlackWire
{
    public static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    /// <summary>The 'error' of an ok:false answer when it is a short lowercase slug ('invalid_auth'); anything else reads as 'unknown', so a reply that repeats the token can't put it in an error.</summary>
    public static string ErrorSlug(JsonElement root)
        => root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String && e.GetString() is { } slug && Slug.IsMatch(slug) ? slug : "unknown";

    /// <summary>Reads the 'messages' of a page and the cursor of the next one; null when the page lacks the array or a message lacks a ts.</summary>
    public static (List<SlackMessage> Messages, string? Next)? ReadPage(JsonElement root)
    {
        if (!root.TryGetProperty("messages", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var messages = array.EnumerateArray().Select(ReadMessage).ToList();
        return messages.Contains(null) ? null : (messages.OfType<SlackMessage>().ToList(), NextCursor(root));
    }

    private static SlackMessage? ReadMessage(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || Str(e, "ts") is not { } ts || !SlackTs.TryParse(ts, out _))
        {
            return null;
        }

        var replies = e.TryGetProperty("reply_count", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var n) ? Math.Max(0, n) : 0;
        return new SlackMessage(ts, Str(e, "thread_ts"), Author(e), Str(e, "text") ?? string.Empty, replies, Str(e, "subtype"), Str(e, "latest_reply") is { } latest && SlackTs.TryParse(latest, out _) ? latest : null);
    }

    /// <summary>The display name of the author: the profile's display name, then its real name, then a bot's username or profile name. The user id is never used.</summary>
    private static string? Author(JsonElement e)
    {
        if (e.TryGetProperty("user_profile", out var profile) && profile.ValueKind == JsonValueKind.Object)
        {
            if (Str(profile, "display_name") is { Length: > 0 } display)
            {
                return display;
            }

            if (Str(profile, "real_name") is { Length: > 0 } real)
            {
                return real;
            }
        }

        if (Str(e, "username") is { Length: > 0 } username)
        {
            return username;
        }

        return e.TryGetProperty("bot_profile", out var bot) && bot.ValueKind == JsonValueKind.Object && Str(bot, "name") is { Length: > 0 } name ? name : null;
    }

    private static string? NextCursor(JsonElement root)
        => root.TryGetProperty("response_metadata", out var meta) && meta.ValueKind == JsonValueKind.Object && Str(meta, "next_cursor") is { Length: > 0 } next ? next : null;

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [GeneratedRegex(@"^[a-z0-9_]{1,64}\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Slug { get; }
}
