using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of 'POST /sindri/sessions' and 'DELETE /sindri/sessions/{actId}/worktree' (agents spec §15.8, A7): the refusals that need no tmux and no agent: a bad act id (400, no agent code), a prompt over 8,000
/// characters (SND-AGENT-017), an agent session as the caller (SND-AUTH-002) and a worktree no start recorded (SND-AGENT-018). The 2xx start needs a real tmux, so the unit tests cover it on a fake host.
/// </summary>
internal static class AgentStartScenarios
{
    private const string Start = "/sindri/sessions";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("sessions-start", "bad-act-id"), ("sessions-start", "empty-act-id"), ("sessions-start", "prompt-too-long"), ("sessions-start", "session-refused"), ("sessions-worktree", "unrecorded"), ("sessions-last-start", "present"), ("sessions-last-start", "absent"),
    ];

    /// <summary>Whether this class owns the scenario.</summary>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>True when it does.</returns>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the answer's body.</summary>
    /// <param name="host">The golden host.</param>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The body.</returns>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario)
    {
        ArgumentNullException.ThrowIfNull(host);
        var owner = Human(host.Token.Value);
        switch ((slug, scenario))
        {
            case ("sessions-start", "bad-act-id"):
                return await Ask(host, Post(Start, Json("short", "hi"), owner));
            case ("sessions-start", "empty-act-id"):   // the id type refuses it while the body is read: a 400 from ApiProblems, not a 500
                return await Ask(host, Post(Start, Json("", "hi"), owner));
            case ("sessions-start", "prompt-too-long"):
                return await Ask(host, Post(Start, Json("k3j9x0a1", new string('x', 8001)), owner));
            case ("sessions-start", "session-refused"):
                using (AsSession())
                {
                    return await Ask(host, Post(Start, Json("k3j9x0a1", "hi"), Human(host.Token.Value)));
                }

            case ("sessions-last-start", "present"):
                await File.WriteAllTextAsync(Path.Combine(host.Home, "last-start.json"), """{"repo":"demo","agent":"codex"}""", TestContext.Current.CancellationToken);
                return await Ask(host, Get("/sindri/sessions/last-start", owner));
            case ("sessions-last-start", "absent"):
                return await Ask(host, Get("/sindri/sessions/last-start", owner));
            case ("sessions-worktree", "unrecorded"):
                return await Ask(host, Delete("/sindri/sessions/never0000/worktree", owner));
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "not an agent start scenario");
        }
    }

    private static string Json(string actId, string prompt) => $$"""{"repo":"demo","agent":"claude-code","prompt":"{{prompt}}","worktree":false,"actId":"{{actId}}"}""";

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;
}
