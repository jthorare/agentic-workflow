using Aw.Sindri.Tests.Messages;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Messages goldens (messages spec §8.2, §14 M1f). <see cref="Built"/> holds the rows whose routes serve the store (lists, conversations, a window and
/// a thread): they are the "Messages" group of <see cref="ApiScenarios.Groups"/>, run by <see cref="MessagesApiGoldenTests"/>. <see cref="Pending"/> holds
/// status and local search, whose services are still stubs until M1f's second PR; they join the group with their golden files. Every scenario but the
/// empty list seeds <see cref="MessageSeed"/> through the host's own store; each request is the owner's, except the session row's.
/// </summary>
internal static class MessageScenarios
{
    /// <summary>The rows the routes serve now, each with its golden file.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> Built { get; } =
    [
        ("messages-lists-id", "needs-reply"), ("messages-lists-id", "unread"), ("messages-lists-id", "needs-reply-empty"), ("messages-lists-id", "session-needs-reply"),
        ("messages-conversations", "some"),
        ("messages-conversations-id-messages", "with-gap"),
        ("messages-threads-id-id", "root-and-replies"),
    ];

    /// <summary>The rows of M1f's second PR (status, local search): no golden file yet.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> Pending { get; } =
    [
        ("messages-status", "polling"),
        ("messages-search", "local"),
    ];

    /// <summary>The (slug, scenario) pairs of this class: one per list, its empty state and a session's reading.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [.. Built, .. Pending];

    /// <summary>The list rows only (the first red test of M1f, 'ApiGoldens_messages_lists').</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> Lists { get; } = [.. All.Where(static r => r.Slug == "messages-lists-id")];

    /// <summary>Whether this class owns a row of the group.</summary>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>True when it does.</returns>
    public static bool Handles(string slug, string scenario) => Built.Contains((slug, scenario));

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

    /// <summary>Seeds the row's store (all but the empty list) and returns the answer's body.</summary>
    /// <param name="host">The golden host.</param>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The body.</returns>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (scenario != "needs-reply-empty")
        {
            await MessageSeed.Seed(host.App.Services, host.Home, ct);
        }

        if (scenario != "session-needs-reply")
        {
            return (await host.Http.Ask(Get(Path(slug, scenario), Human(host.Token.Value)))).Body;
        }

        using (AsSession())   // why: Human reads the scope when it builds the headers, so the request is built inside it
        {
            return (await host.Http.Ask(Get(Path(slug, scenario), Human(host.Token.Value)))).Body;
        }
    }
}
