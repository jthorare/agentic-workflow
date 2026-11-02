using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Obligations;

/// <summary>Decides whether a send was addressed to a given peer: by session name, session id or socket.</summary>
internal static class Recipient
{
    internal static bool Matches(string to, string name, string? other = null)
    {
        to = StripRef(to);
        return to.Length > 0
            && (string.Equals(to, name, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(other) && string.Equals(to, other, StringComparison.OrdinalIgnoreCase)));
    }

    // A send to "name [ref]" (SendMessage's form when a bare name matches two sessions) is addressed to "name".
    private static string StripRef(string to)
    {
        if (to.Length < 4 || to[^1] != ']')
        {
            return to;
        }

        var open = to.LastIndexOf(" [", StringComparison.Ordinal);
        return open > 0 && open < to.Length - 3 && to.IndexOf(']', open) == to.Length - 1
            ? to[..open]
            : to;
    }

    internal static bool Matches(string to, LaneAssignment lane) =>
        Matches(to, lane.SessionName, lane.Session?.Value);

    // A marker such as "LANE MAP v2" or "OWNER ANSWER q3" must not be a prefix of a longer number ("v20").
    internal static bool Contains(string text, string marker)
    {
        for (var from = 0; from < text.Length;)
        {
            var at = text.IndexOf(marker, from, StringComparison.Ordinal);
            if (at < 0)
            {
                return false;
            }

            var end = at + marker.Length;
            if (end >= text.Length || !char.IsAsciiDigit(text[end]))
            {
                return true;
            }

            from = end;
        }

        return false;
    }
}
