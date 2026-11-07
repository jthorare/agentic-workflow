using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The list rows (Ph5a) and the PR page rows (Ph5c) are registered.
/// The Reviews page's goldens (PR review page spec §6.1-6.3, §9, §16): 'GET /reviews' in each list state and 'GET /reviews/{owner}/{repo}/{number}' in each page state. Hand-written first
/// (Ph5 red): the routes don't exist yet, so every row answers 404 against its golden. The Ph5 implementation seeds what each row's `// seeded in Ph5:` note names.
/// </summary>
internal static class ReviewScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("reviews", "some"), ("reviews", "empty"), ("reviews", "no-login"), ("reviews", "github-failing"), ("reviews", "before-reviewers"), ("reviews", "library-unreadable"), ("reviews", "session"),
        ("reviews-pr", "full"), ("reviews-pr", "empty-panels"), ("reviews-pr", "panel-errors"), ("reviews-pr", "moving"), ("reviews-pr", "offline-cached"),
        ("reviews-pr", "own-pr"), ("reviews-pr", "agent-pr"), ("reviews-pr", "capped"), ("reviews-pr", "not-synced"), ("reviews-pr", "no-login"), ("reviews-pr", "not-found"), ("reviews-pr", "offline-uncached"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    internal const string GithubContext = "context:\n  sources:\n    github: { repos: [acme/api, acme/web] }\n";
    internal const string SindriProject = "prj_0000000000000000000000000A";
    private const string ReviewBlock = "review:\n  login: joi-example\n  projects:\n    - { project: prj_0000000000000000000000000A, repos: [acme/api] }\n";

    private static readonly string[] _heads = [new('a', 40), new('b', 40), new('c', 40), new('d', 40)];

    /// <summary>Starts the scenario's host: the item profile with the github repos (and the review block unless the scenario is 'no-login'), over the scenario's stored PR records, project names and forge login.</summary>
    public static Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct) =>
        slug != "reviews"
            ? ReviewPageScenarios.Start(scenario, ct)
            : GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml + GithubContext + (scenario == "no-login" ? string.Empty : ReviewBlock), extra: services =>
            {
                _ = services.Replace(ServiceDescriptor.Singleton<IReviewFacts>(Facts(scenario)));
                _ = services.Replace(ServiceDescriptor.Singleton<IReviewProjects>(new FakeReviewProjects().With(SindriProject, "Sindri")));
                _ = services.Replace(ServiceDescriptor.Singleton<IProjectPages>(new OneProjectPage()));   // the setup approval checks 'review.projects' against the Projects page (SND-REVIEW-002)
                _ = services.Replace(ServiceDescriptor.Singleton<IPullRequests>(new FakePullRequests(["github.com"]) { SelfLogin = "joi-example" }));
            });

    /// <summary>The stored PR records and sources of a 'reviews' row: 'some' and its variants share four open PRs (acme/api#2071 asks for the owner's review, acme/web#880 was reviewed with 2 commits since, acme/api#2064 and the draft acme/web#875 are the owner's).</summary>
    private static FakeReviewFacts Facts(string scenario)
    {
        var read = scenario != "before-reviewers";
        List<ReviewPull> pulls = scenario == "empty" ? [] :
        [
            Pull("acme/api", 2071, "Add retry budget to the sync worker", "sam-dev", draft: false, (2025, 12, 29, 12), (2025, 12, 31, 12), _heads[0], read ? ["joi-example"] : [], [], read, [new ReviewClaim("sindri-pr-review#Ph5", Resolved: true)]),
            Pull("acme/web", 880, "Fold the settings tabs into one page", "kim-dev", draft: false, (2025, 12, 27, 12), (2025, 12, 30, 12), _heads[1], [], read ? [new ReviewedAt("joi-example", _heads[3], NewCommits: 2)] : [], read, []),
            Pull("acme/api", 2064, "Cache the repo list between syncs", "joi-example", draft: false, (2025, 12, 28, 12), (2025, 12, 31, 12), _heads[2], [], [], read, [new ReviewClaim("sindri-pr-review#Ph4", Resolved: true)]),
            Pull("acme/web", 875, "Draft: new empty state for Work", "joi-example", draft: true, (2025, 12, 30, 12), (2025, 12, 30, 12), head: null, [], [], read, []),
        ];
        var synced = KnownInstants.Epoch.AddMinutes(-14.4);
        var github = new ProjectSourceFreshness("github", Usable: true, synced, scenario == "github-failing" ? "SND-INDEX-401" : null);
        var library = scenario == "library-unreadable" ? new ReviewLibraryFacts(Reading: false, 33, WithErrors: 0, [new ReviewUnreadableRoot("~/notes/vault", "permission denied")]) : new ReviewLibraryFacts(Reading: false, 41, WithErrors: 3, []);
        return new FakeReviewFacts().With(new ReviewFactsSnapshot(pulls, github, library));
    }

    private static ReviewPull Pull(string repo, int number, string title, string author, bool draft, (int, int, int, int) opened, (int, int, int, int) updated, string? head,
        IReadOnlyList<string> requested, IReadOnlyList<ReviewedAt> reviews, bool read, IReadOnlyList<ReviewClaim> claims) =>
        new(repo, number, title, author, draft, At(opened), At(updated), head, requested, reviews, read, claims);

    private static DateTimeOffset At((int Year, int Month, int Day, int Hour) t) => new(t.Year, t.Month, t.Day, t.Hour, 0, 0, TimeSpan.Zero);

    /// <summary>Stores the per-head summaries the list reads (no forge call is needed): acme/api#2071 CI failing with 2 open checks, acme/web#880 passing with none open, acme/api#2064 running with 1 open; the draft has none.</summary>
    private static async Task SeedSummaries(GoldenHost host, CancellationToken ct)
    {
        var cache = host.App.Services.GetRequiredService<IReviewSummaries>();
        foreach (var (repo, number, head, ci, open) in new (string, int, string, ReviewCi, int)[] { ("acme/api", 2071, _heads[0], ReviewCi.Fail, 2), ("acme/web", 880, _heads[1], ReviewCi.Pass, 0), ("acme/api", 2064, _heads[2], ReviewCi.Running, 1) })
        {
            _ = (await cache.Write(new ReviewPullRef(repo[..repo.IndexOf('/', StringComparison.Ordinal)], repo[(repo.IndexOf('/', StringComparison.Ordinal) + 1)..], number), new ReviewSummary(head, ci, open, KnownInstants.Epoch), ct)).Value;
        }
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        if (slug == "reviews")
        {
            await SeedSummaries(host, ct);
        }

        if (slug == "reviews-pr")
        {
            return await ReviewPageScenarios.Body(host, scenario);
        }

        // seeded in Ph5: list rows ('some': acme/api#2071 requested, acme/web#880 reviewed with 2 new commits, acme/api#2064 and draft acme/web#875 by joi-example; 'empty': none;
        // 'github-failing': the last rows plus a failing github freshness; 'before-reviewers': PR records without reviewers), the library (41 docs, 3 with errors; 'library-unreadable': 33 docs and one unreadable root)
        // all at the epoch clock
        var path = "/sindri/reviews";
        var caller = scenario == "session" ? ItemScenarios.Session("demo") : ItemScenarios.Human(host);
        return (await host.Http.Ask(Get(path, caller))).Body;
    }

    /// <summary>A Projects page holding only the card the review block names, so the setup approval passes SND-REVIEW-002.</summary>
    internal sealed class OneProjectPage : IProjectPages
    {
        public ValueTask<Result<ProjectsPage>> Read(bool all, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ProjectsPage>>(new ProjectsPage(Empty: null, Viewer: null, Freshness: [],
                [new ProjectCard(SindriProject, ProjectOrigin.HandMade, "Sindri", Description: null, State: null, StateSetByOwner: false, Health: null, Lead: null, Target: null, OpenIssues: null,
                    Counting: false, OpenPrs: null, PrScope: null, LastActivity: null, FinishedAt: null, Repos: [], Links: [], Notes: null, Synced: true, Version: 1)],
                Finished: [], Hidden: []));
    }
}
