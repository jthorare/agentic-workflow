namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Messages goldens (messages spec §8.2, §14 M1f): the rows M1f records. They are NOT in <see cref="ApiScenarios.Groups"/> yet: a row there needs its
/// golden file (<see cref="ApiGoldenTests.Every_golden_file_has_a_scenario"/>) and joins the session and attribution sweeps, and no file can be recorded
/// while the routes are stubs. M1f records each file (AW_GOLDEN_UPDATE=1, then rebuild), adds a "Messages" group with its MessagesApiGoldenTests class,
/// and moves these rows there. Until then Messages.MessagesReadRouteTests runs them red.
/// </summary>
internal static class MessageScenarios
{
    /// <summary>The (slug, scenario) pairs of this class: one per list, its empty state and a session's reading.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("messages-lists-id", "needs-reply"), ("messages-lists-id", "unread"), ("messages-lists-id", "needs-reply-empty"), ("messages-lists-id", "session-needs-reply"),
        ("messages-status", "polling"),
        ("messages-conversations", "some"),
        ("messages-conversations-id-messages", "with-gap"),
        ("messages-threads-id-id", "root-and-replies"),
        ("messages-search", "local"),
    ];

    /// <summary>The list rows only (the first red test of M1f, 'ApiGoldens_messages_lists').</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> Lists { get; } = [.. All.Where(static r => r.Slug == "messages-lists-id")];

    /// <summary>The route a row asks.</summary>
    /// <param name="slug">The slug.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The path under the host.</returns>
    public static string Path(string slug, string scenario) => (slug, scenario) switch
    {
        ("messages-lists-id", "unread") => "/sindri/messages/lists/unread",
        ("messages-lists-id", _) => "/sindri/messages/lists/needs-reply",
        ("messages-status", _) => "/sindri/messages/status",
        ("messages-conversations", _) => "/sindri/messages/conversations",
        ("messages-conversations-id-messages", _) => "/sindri/messages/conversations/C0HARBOR/messages",
        ("messages-threads-id-id", _) => "/sindri/messages/threads/C0HARBOR/1767771000.000100",
        _ => "/sindri/messages/search?q=fare&scope=local",
    };
}
