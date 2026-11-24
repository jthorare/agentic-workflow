using System.Text.Json.Nodes;

namespace Aw.Connectors.Testing.Slack;

/// <summary>A person as Slack's 'users.info' sends it.</summary>
internal static class SlackUserJson
{
    public static JsonObject Of(SlackUserSeed u, string teamId) => new()
    {
        ["id"] = u.Id,
        ["team_id"] = teamId,
        ["name"] = u.Name,
        ["deleted"] = u.Deleted,
        ["real_name"] = u.RealName,
        ["tz"] = u.TimeZone,
        ["is_bot"] = u.IsBot,
        ["profile"] = new JsonObject
        {
            ["display_name"] = u.DisplayName,
            ["real_name"] = u.RealName,
            ["title"] = u.Title,
            ["status_text"] = u.StatusText,
        },
    };
}
