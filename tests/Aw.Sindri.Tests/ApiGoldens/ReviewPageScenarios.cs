using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The PR page's goldens (PR review page spec §6.2, §6.3, §9; Ph5c): 'GET /reviews/{owner}/{repo}/{number}' in each page state. The page is built over a fake forge (acme/api#2071 with five files, a failing and a passing run,
/// one thread), a fake stored side (the claims, the doc map, the test plan) and a fake evidence lookup, all at the epoch clock; the settings are the project's 'sindri' entry of the scenario's profile.
/// </summary>
internal static class ReviewPageScenarios
{
    private const string Head = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    private const string Link = "https://github.com/acme/api/pull/2071";
    private const string PagePath = "/sindri/reviews/acme/api/2071";

    // why: every check has a 'when', so a PR that changes no files has none that apply ('empty-panels' reads "No required checks for this project")
    private const string ReviewBlock =
        "review:\n  login: joi-example\n  projects:\n    - { project: prj_0000000000000000000000000A, repos: [acme/api], settings: sindri }\n  settings:\n    sindri:\n"
        + "      panels: [requiredChecks, docMap, testPlan, ci, evidence, risk, conversation]\n"
        + "      groups:\n        - { name: Core, globs: [\"src/**\"] }\n        - { name: Assets, globs: [\"assets/**\"] }\n"
        + "      collapse: [\"assets/**\"]\n"
        + "      risky:\n        - { globs: [\"src/Api/Migrations/**\"], why: \"Migrations change stored data\" }\n"
        + "      checks:\n"
        + "        - { id: ci-ui, title: \"Recipe change shows Ui tests ran\", when: [\"src/**\"], need: { ci: ui-tests } }\n"
        + "        - { id: owner-ran-it, title: \"I ran it myself\", when: [\"src/**\"], need: { manual: true } }\n"
        + "        - { id: build, title: \"Build passes\", when: [\"src/**\"], need: { ci: build } }\n"
        + "        - { id: body-proof, title: \"Proof is in the PR body\", when: [\"src/**\"], need: { body: \"^proof: .+$\" } }\n"
        + "        - { id: lint, title: \"Lint passes\", when: [\"src/**\"], need: { ci: lint } }\n";

    private const string Profile = ItemScenarios.ProfileYaml + ReviewScenarios.GithubContext + ReviewBlock;
    private const string ProfileNoLogin = ItemScenarios.ProfileYaml + ReviewScenarios.GithubContext;

    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, scenario == "no-login" ? ProfileNoLogin : Profile, extra: services =>
        {
            var forge = Forge(scenario);
            _ = services.Replace(ServiceDescriptor.Singleton<IPullRequests>(forge));
            _ = services.Replace(ServiceDescriptor.Singleton<IReviewPageStore>(new FakeReviewPageStore().Syncing("acme/api", "acme/web").With(Stored(scenario))));
            _ = services.Replace(ServiceDescriptor.Singleton<IReviewEvidence>(new FakeReviewEvidence().With(Evidence(scenario))));
            _ = services.Replace(ServiceDescriptor.Singleton<IReviewProjects>(new FakeReviewProjects().With(ReviewScenarios.SindriProject, "Sindri")));
            _ = services.Replace(ServiceDescriptor.Singleton<IProjectPages>(new ReviewScenarios.OneProjectPage()));   // the setup approval checks 'review.projects' against the Projects page (SND-REVIEW-002)
        });

    public static async Task<string> Body(GoldenHost host, string scenario)
    {
        var caller = ItemScenarios.Human(host);
        var path = scenario switch
        {
            "not-synced" => "/sindri/reviews/acme/other/12",
            "not-found" => "/sindri/reviews/acme/api/9999",
            _ => PagePath,
        };
        if (scenario == "offline-cached")
        {
            // the first read is online and caches the page; the forge then goes away for the second
            _ = await host.Http.Ask(Get(path, caller));
            var forge = (FakePullRequests)host.App.Services.GetRequiredService<IPullRequests>();
            forge.GetFailure = SindriErrors.ForgeUnavailable.With("github can't be reached");
            ((FakeTimeProvider)host.App.Services.GetRequiredService<TimeProvider>()).Advance(TimeSpan.FromMinutes(14.4));
        }

        return (await host.Http.Ask(Get(path, caller))).Body;
    }

    private static DateTimeOffset At(int month, int day, int hour) => new(month == 12 ? 2025 : 2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private static PullFile[] Files(string scenario)
    {
        var migration = new PullFile("src/Api/Migrations/0042_retry.sql", PreviousPath: null, PullFileStatus.Added, Additions: 2, Deletions: 0, "@@ -0,0 +1,2 @@\n+alter table retry add column budget int;\n+update retry set budget = 5;");
        return scenario switch
        {
            "empty-panels" => [],
            "capped" => [new PullFile("src/Api/Generated/Schema.g.cs", PreviousPath: null, PullFileStatus.Modified, Additions: 12000, Deletions: 3, new string('+', (1024 * 1024) + 1)), migration],
            _ =>
            [
                new PullFile("src/Api/A/A.Core/X.cs", PreviousPath: null, PullFileStatus.Modified, Additions: 4, Deletions: 1, "@@ -10,3 +10,6 @@\n var a = 1;\n-var b = 2;\n+var b = 3;\n+var c = 4;\n+var d = 5;\n+var e = 6;"),
                new PullFile("src/Api/A/A.Core/Budget.cs", "src/Api/A/A.Core/Limit.cs", PullFileStatus.Renamed, Additions: 2, Deletions: 2, "@@ -1,2 +1,2 @@\n-class Limit {}\n+class Budget {}", Similarity: 91),
                new PullFile("src/Api/A/A.Core/Old.cs", PreviousPath: null, PullFileStatus.Removed, Additions: 0, Deletions: 61, "@@ -1,3 +0,0 @@\n-class Old {}\n-// legacy\n-// removed"),
                new PullFile("assets/budget.png", PreviousPath: null, PullFileStatus.Added, Additions: 0, Deletions: 0, Patch: null),
                migration,
            ],
        };
    }

    private static FakePullRequests Forge(string scenario)
    {
        var forge = new FakePullRequests(["github.com"]) { SelfLogin = "sindri-agent" };
        var pull = forge.Add(new PullRepo("github.com", "acme", "api"), 2071, "feat/retry-budget", Head);
        pull.Title = "Add retry budget to the sync worker";
        pull.Author = scenario switch { "own-pr" => "joi-example", "agent-pr" => "sindri-agent", _ => "sam-dev" };
        pull.Body = scenario == "empty-panels" ? string.Empty : "Covers: sindri-pr-review#Ph5, sindri-pr-review#REQ-E3\n\nproof: ran it on the fixture\n\n## Evidence\nSee the comment below.";
        forge.SeedFiles(pull.Link, Files(scenario), new Dictionary<string, string>(StringComparer.Ordinal), baseTexts: null, capReached: scenario == "capped");
        Seed(pull, scenario);
        Script(forge, scenario);
        return forge;
    }

    private static void Seed(FakePull pull, string scenario)
    {
        if (scenario != "panel-errors")
        {
            pull.Checks = scenario == "empty-panels"
                ? new PullChecks(PullChecksState.None, Head, [], Passed: 0, Pending: 0, Runs: [])
                : new PullChecks(PullChecksState.Failing, Head, ["ui-tests"], Passed: 1, Pending: 0, Runs: [new PullCheckRun("ui-tests", "github-actions", "failure", Head), new PullCheckRun("lint", "github-actions", "success", Head)]);
        }

        if (scenario != "empty-panels")
        {
            pull.Threads.Add(new PullThread("thr_1", PullCommentKind.Thread, "kim-dev", "MEMBER", IsBot: false, Resolved: false, Outdated: false, "src/Api/A/A.Core/X.cs", "Should this cap at 5 retries?", [], At(12, 31, 21), Line: 12, EditedAt: null, Url: null));
        }
    }

    private static void Script(FakePullRequests forge, string scenario)
    {
        switch (scenario)
        {
            case "panel-errors":
                forge.FilesFailure = SindriErrors.ForgeUnavailable.With("github returned 502");
                forge.ThreadsFailure = SindriErrors.ForgeUnavailable.With("github returned 502");
                break;
            case "moving":
                // the head moves on the first three reads and settles on the page's own: moved twice, so the newest reads are shown with the banner
                foreach (var moved in new[] { new string('1', 40), new string('2', 40), new string('3', 40), Head })
                {
                    forge.HeadScript.Enqueue(moved);
                }

                break;
            case "offline-uncached":
                forge.GetFailure = SindriErrors.ForgeUnavailable.With("github can't be reached");
                break;
            default:
                break;
        }
    }

    private static ReviewPageEvidenceItem[] Evidence(string scenario) =>
        scenario == "empty-panels" ? [] : [new ReviewPageEvidenceItem("uiEvidence", "Reviews list, 1,440 px", "evidence/2071/reviews-list.png", Link + "#issuecomment-1", "sha", At(12, 31, 22))];

    private static ReviewPageStored Stored(string scenario)
    {
        var library = scenario switch
        {
            "empty-panels" => new ReviewPageLibraryFacts(Error: null, [], Doc: null, [new ReviewPageProjectDoc("sindri-pr-review", "Sindri PR review page", "docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design.md")], [], Hidden: 0),
            "panel-errors" => new ReviewPageLibraryFacts("/home/joi/docs can't be read", [], Doc: null, [], [], Hidden: 0),
            _ => FullLibrary(),
        };
        // why: the store holds acme/api#2071 only, so the read of #9999 finds no stored record and the forge is asked for #9999's own link
        return scenario == "not-found"
            ? new ReviewPageStored(Title: null, Author: null, Draft: false, Url: null, UpdatedAt: null, [], library)
            : new ReviewPageStored("Add retry budget to the sync worker", "sam-dev", Draft: false, Link, At(12, 31, 20), ["ENG-12"], library);
    }

    private static ReviewPageLibraryFacts FullLibrary()
    {
        var doc = new ReviewPageDocFact("sindri-pr-review", "Sindri PR review page", "docs/superpowers/specs/2026-11-04-sindri-pr-review-page-design.md",
        [
            new ReviewPageItemFact("Ph4", "The review settings and library", "phase", Parent: null, ClaimedHere: false, ClaimedBy: [new ReviewPageClaimedBy(2060, Merged: true, "claimed by #2060, merged")]),
            new ReviewPageItemFact("Ph5", "The review routes", "phase", Parent: null, ClaimedHere: true, ClaimedBy: []),
            new ReviewPageItemFact("REQ-E3", "Every PR route answers with its panels", "req", "Ph5", ClaimedHere: true, ClaimedBy: []),
            new ReviewPageItemFact("REQ-E4", "Offline PRs answer from the cache", "req", "Ph5", ClaimedHere: false, ClaimedBy: []),
        ]);
        return new ReviewPageLibraryFacts(
            Error: null, [new ReviewPageClaim("sindri-pr-review#Ph5", "resolved", Error: null), new ReviewPageClaim("sindri-pr-review#REQ-E3", "resolved", Error: null)], doc, [],
            [
                new ReviewPageTestRow("TP3", "GET /reviews/acme/api/2071 answers every panel", ["REQ-E3"], Ticked: false),
                new ReviewPageTestRow("TP4", "An offline PR answers from the cache", ["REQ-E4"], Ticked: false),
            ],
            Hidden: 0);
    }
}
