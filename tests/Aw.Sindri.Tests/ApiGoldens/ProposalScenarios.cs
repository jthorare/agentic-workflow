using Aw.Sindri.Core.Evolve;
using Aw.Sindri.Tests.Proposals;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of the proposal routes (issue 3787, piece 3): 'GET /sindri/proposals[?state=]', 'GET /sindri/proposals/{id}' and the owner's 'POST /sindri/proposals/{id}/reject'.
/// A scenario seeds proposals through the host's store; the ids come from the golden host's id sequence.
/// </summary>
internal static class ProposalScenarios
{
    private const string Missing = "/sindri/proposals/prp_00000000000000000000000000000000";

    private static readonly string[] _empty = [];
    private static readonly string[] _two = ["Run the tests before saying done", "Ask before widening the scope"];

    // slug, scenario, the titles to seed, the method, the path (with {0} for the first seeded id), as the owner or a session
    private static readonly (string Slug, string Scenario, string[] Seed, string Method, string Path, bool Session)[] _rows =
    [
        ("proposals", "empty", _empty, "GET", "/sindri/proposals", false),
        ("proposals", "listed", _two, "GET", "/sindri/proposals", false),
        ("proposals", "by-state", _two, "GET", "/sindri/proposals?state=held", false),
        ("proposals", "bad-state", _empty, "GET", "/sindri/proposals?state=lost", false),
        ("proposals-id", "found", _two, "GET", "/sindri/proposals/{0}", false),
        ("proposals-id", "not-found", _empty, "GET", Missing, false),
        ("proposals-id", "bad-id", _empty, "GET", "/sindri/proposals/nope", false),
        ("proposals-id-reject", "rejected", _two, "POST", "/sindri/proposals/{0}/reject", false),
        ("proposals-id-reject", "not-found", _empty, "POST", Missing + "/reject", false),
        ("proposals-id-reject", "session-refused", _two, "POST", "/sindri/proposals/{0}/reject", true),
    ];

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [.. _rows.Select(static r => (r.Slug, r.Scenario))];

    /// <summary>Whether this class owns the scenario.</summary>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>True when it does.</returns>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the answer's body.</summary>
    /// <param name="host">The golden host.</param>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The body.</returns>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        var row = _rows.Single(r => r.Slug == slug && r.Scenario == scenario);
        var store = host.App.Services.GetRequiredService<IProposalStore>();
        var first = string.Empty;
        foreach (var title in row.Seed)
        {
            var saved = (await store.Save(ProposalSeeds.Saving(title), ct)).Value;
            first = first.Length == 0 ? saved.Id.Value : first;
        }

        var path = row.Path.Replace("{0}", first, StringComparison.Ordinal);
        if (!row.Session)
        {
            var owner = Human(host.Token.Value);
            return (await host.Http.Ask(row.Method == "GET" ? Get(path, owner) : Post(path, json: null, owner))).Body;
        }

        using (AsSession())
        {
            return (await host.Http.Ask(Post(path, json: null, Human(host.Token.Value)))).Body;
        }
    }
}
