using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of 'POST /sindri/sessions' 'DELETE /sindri/sessions/{actId}/worktree' and 'POST /sindri/sessions/{provider}/{id}/stop' (agents spec §15.8, A7; issue 3147): the refusals that need no tmux and no agent: a bad act id (400, no agent code), a prompt over 8,000
/// characters (SND-AGENT-017), an agent session as the caller (SND-AUTH-002) and a worktree no start recorded (SND-AGENT-018). The 2xx start needs a real tmux, so the unit tests cover it on a fake host.
/// </summary>
internal static class AgentStartScenarios
{
    private const string Start = "/sindri/sessions";

    /// <summary>The slug of GET /sindri/sessions/{provider}/{id}/terminal (the WebSocket route): only its refusals before the upgrade are plain HTTP, so only they have goldens.</summary>
    private const string Terminal = "sessions-id-id-terminal";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("sessions-start", "bad-act-id"), ("sessions-start", "empty-act-id"), ("sessions-start", "prompt-too-long"), ("sessions-start", "session-refused"), ("sessions-worktree", "unrecorded"), ("sessions-id-id-stop", "not-started"), ("sessions-id-id-stop", "session-refused"), ("sessions-last-start", "present"), ("sessions-last-start", "absent"),
        (Terminal, "bad-ref"), (Terminal, "no-pane"), (Terminal, "pane-gone"),
    ];

    /// <summary>Whether this class owns the scenario.</summary>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>True when it does.</returns>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Whether the scenario needs the host with a fake pane locator.</summary>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>True for the terminal route's no-pane and pane-gone rows.</returns>
    public static bool NeedsLocator(string slug, string scenario) => string.Equals(slug, Terminal, StringComparison.Ordinal) && (string.Equals(scenario, "no-pane", StringComparison.Ordinal) || string.Equals(scenario, "pane-gone", StringComparison.Ordinal));

    /// <summary>Starts the host of a terminal row: the pane locator answers SND-AGENT-015 (a Warp session) or SND-AGENT-016 for any ref.</summary>
    /// <param name="scenario">The scenario.</param>
    /// <param name="ct">Cancels.</param>
    /// <returns>The host.</returns>
    public static Task<GoldenHost> StartHost(string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, extra: services => services.Replace(ServiceDescriptor.Singleton<ITerminalPaneLocator>(new RefusingLocator(
            scenario == "no-pane"
                ? SindriErrors.AgentNoPane.With("the session runs in Warp (warp), not in a tmux pane")
                : SindriErrors.AgentPaneGone.With("no live session has that ref")))));

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
            case ("sessions-id-id-stop", "not-started"):
                return await Ask(host, Post("/sindri/sessions/claude-code/sindri-zzzzzzzz/stop", null, owner));
            case ("sessions-id-id-stop", "session-refused"):
                using (AsSession())
                {
                    return await Ask(host, Post("/sindri/sessions/claude-code/sindri-zzzzzzzz/stop", null, Human(host.Token.Value)));
                }

            case (Terminal, _):
                return await TerminalBody(host, scenario);
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "not an agent start scenario");
        }
    }

    private static async Task<string> TerminalBody(GoldenHost host, string scenario)
    {
        var owner = Human(host.Token.Value);
        // Plain GETs: the ref and the pane lookup both answer before the upgrade check, so the refusal comes back as plain HTTP (#3563).
        return string.Equals(scenario, "bad-ref", StringComparison.Ordinal)
            ? await Ask(host, Get("/sindri/sessions/claude-code/a..b/terminal", owner))
            : await Ask(host, Get("/sindri/sessions/claude-code/live/terminal", owner));
    }

    private sealed class RefusingLocator(Error error) : ITerminalPaneLocator
    {
        public ValueTask<Result<TerminalTarget>> Find(SessionRef session, CancellationToken cancellationToken) => ValueTask.FromResult<Result<TerminalTarget>>(error);
    }

    private static string Json(string actId, string prompt) => $$"""{"repo":"demo","agent":"claude-code","prompt":"{{prompt}}","worktree":false,"actId":"{{actId}}"}""";

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;
}
