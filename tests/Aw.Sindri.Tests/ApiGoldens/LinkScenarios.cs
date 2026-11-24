using Aw.Kernel;
using Aw.Sindri.Core.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of QUERY /sindri/links/resolve (messages spec §9.3, M1g): one body of a GitHub PR url and a url with no known shape, answered by the registered store
/// ('none': it holds no rows until Home's row store lands) and by a store that holds the PR's row (title, state, CI, waits-on, since). Every request is the owner's.
/// </summary>
internal static class LinkScenarios
{
    private const string Slug = "links-resolve";
    private const string Body = """{"urls":["https://github.com/acme/harbor/pull/3120","https://example.com/x"]}""";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [(Slug, "none"), (Slug, "pr-row")];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>True when it does.</returns>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the registered store for 'none', a store holding the PR's row for 'pr-row'.</summary>
    /// <param name="scenario">The scenario.</param>
    /// <param name="ct">Cancels the start.</param>
    /// <returns>The host.</returns>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) =>
        scenario == "pr-row"
            ? GoldenHost.Start(temporary: true, ct, extra: static services =>
            {
                _ = services.RemoveAll<ILinkRows>();
                _ = services.AddSingleton<ILinkRows>(new OnePrRow());
            })
            : GoldenHost.Start(temporary: true, ct);

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the answer's body.</summary>
    /// <param name="host">The golden host.</param>
    /// <returns>The body.</returns>
    public static async Task<string> Answer(GoldenHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return (await host.Http.Ask(Query("/sindri/links/resolve", Body, Human(host.Token.Value)))).Body;
    }

    // why: Home's row store isn't built, so the scenario registers a store that holds one PR row with every field, at a fixed time
    private sealed class OnePrRow : ILinkRows
    {
        private static readonly LinkRow _row = new("gh:pr:acme/harbor#3120", "Fare table uses 90 s", "open")
        {
            Ci = "passing",
            WaitsOn = "you",
            Since = new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero),
        };

        public ValueTask<Result<IReadOnlyDictionary<string, LinkRow>>> Find(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
        {
            var found = new Dictionary<string, LinkRow>(StringComparer.Ordinal);
            if (keys.Contains(_row.Key, StringComparer.Ordinal))
            {
                found[_row.Key] = _row;
            }

            return ValueTask.FromResult<Result<IReadOnlyDictionary<string, LinkRow>>>(found);
        }
    }
}
