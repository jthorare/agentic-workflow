using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The fix task send (PR review page spec §8.2): 'POST /reviews/{owner}/{repo}/{number}/fix' with a finding. acme/api#1 is the owner's PR (joi-example, branch feat/retry), acme/api#2 is sam-dev's.
/// The fix-task files are the real ones in the host's temporary home; the babysitter and session lookups are set per scenario.
/// </summary>
internal static class ReviewFixScenarios
{
    private const string Route = "/sindri/reviews/acme/api/";
    private const string Patch = "@@ -40,0 +41,3 @@\n+a\n+b\n+c";
    private const string Owner = "joi-example";

    private static readonly string _head = new('a', 40);
    private static readonly int[] _numbers = [1, 2];

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("reviews-fix", "done"), ("reviews-fix", "half-done"), ("reviews-fix", "item-only"), ("reviews-fix", "to-babysitter"), ("reviews-fix", "to-session"), ("reviews-fix", "waiting"), ("reviews-fix", "old-side"), ("reviews-fix", "session-refused"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile with the github repos and a login, two stored PRs and their forge pulls.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml + "context:\n  sources:\n    github: { repos: [acme/api] }\nreview:\n  login: " + Owner + "\n", extra: services =>
        {
            var forge = new FakePullRequests(["github.com"]) { SelfLogin = "sindri-bot" };
            foreach (var number in _numbers)
            {
                var pull = forge.Add(new PullRepo("github.com", "acme", "api"), number == 1 ? "feat/retry" : "feat/other", _head);
                pull.Files = [new PullFile("src/Sync.cs", PreviousPath: null, PullFileStatus.Modified, 3, 0, Patch)];
            }

            _ = services.Replace(ServiceDescriptor.Singleton<IPullRequests>(forge));
            _ = services.Replace(ServiceDescriptor.Singleton<IReviewFacts>(new FakeReviewFacts().With(new ReviewFactsSnapshot([Pull(1, Owner), Pull(2, "sam-dev")], Github: null, new ReviewLibraryFacts(Reading: false, Docs: 0, WithErrors: 0, [])))));
            _ = services.Replace(ServiceDescriptor.Singleton<IFixBabysitters>(new Babysitters(scenario == "to-babysitter")));
            _ = services.Replace(ServiceDescriptor.Singleton<IFixSessions>(new Sessions(string.Equals(scenario, "to-session", StringComparison.Ordinal) || string.Equals(scenario, "done", StringComparison.Ordinal) ? "claude-code, ~/repos/aw-wt/api-retry" : null)));
        });

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        var line = $$"""{"kind":"line","text":"Guard the null.","head":"{{_head}}","path":"src/Sync.cs","line":42""";
        return scenario switch
        {
            "half-done" => await Send(host, "1", $$"""{"kind":"line","text":"Guard the null.","head":"{{new string('b', 40)}}","path":"src/Sync.cs","line":42}"""),
            "item-only" => await Send(host, "2", line + "}"),
            "old-side" => await Send(host, "1", line + ",\"side\":\"old\"}"),
            "session-refused" => (await host.Http.Ask(Post(Route + "1/fix", line + "}", ItemScenarios.Session("demo")))).Body,
            _ => await Send(host, "1", line + "}"),
        };
        static async Task<string> Send(GoldenHost h, string number, string body) => (await h.Http.Ask(Post(Route + number + "/fix", body, ItemScenarios.Human(h)))).Body;
    }

    private static ReviewPull Pull(int number, string author) =>
        new("acme/api", number, "Add retry budget", author, Draft: false, new DateTimeOffset(2025, 12, 29, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 12, 0, 0, TimeSpan.Zero), _head, [], [], ReviewersRead: true, [],
            Url: "https://github.com/acme/api/pull/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private sealed class Babysitters(bool watching) : IFixBabysitters
    {
        public ValueTask<bool> Watching(string link, CancellationToken cancellationToken) => ValueTask.FromResult(watching);
    }

    private sealed class Sessions(string? session) : IFixSessions
    {
        public ValueTask<string?> OnBranch(string headRepo, string branch, CancellationToken cancellationToken) => ValueTask.FromResult(session);
    }
}
