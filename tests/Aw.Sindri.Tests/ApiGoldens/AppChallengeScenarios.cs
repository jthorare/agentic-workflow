using Aw.Sindri.Host;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>The goldens of the app's challenge route (setup-once spec §4): 'POST /workflow/challenges/app', which confirms 'approve profile' and refuses every other action.</summary>
internal static class AppChallengeScenarios
{
    internal const string Slug = "workflow-challenges-app";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [(Slug, "profile"), (Slug, "refused-action")];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>The scenario's body: the answer to the app's challenge request.</summary>
    public static async Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        (string Name, string Value)[] app = [(SindriHeaders.Caller, "human"), (SindriHeaders.Client, "ui"), (SindriHeaders.Token, host.Token.Value)];
        var action = scenario switch
        {
            "profile" => "approve profile " + (await host.LiveHash(ct)).Value[..6],
            "refused-action" => "approve-all c41e07",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such challenges-app scenario"),
        };
        return (await host.Http.Ask(Post("/sindri/workflow/challenges/app", $$"""{"action":"{{action}}"}""", app))).Body;
    }
}
