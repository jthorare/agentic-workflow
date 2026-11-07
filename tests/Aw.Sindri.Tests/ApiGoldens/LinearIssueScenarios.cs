using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Work → Linear's list (Linear hub spec §3, Ph3 1a; contract ~/.agentic-workflow/digests/api-scripts/contract-2077-ph3-linear-reads.md): 'GET /linear/issues' in each state of the list. The stored issues, the
/// source and the items linked to them are fakes seeded per scenario (<see cref="FakeLinearIssueFacts"/>, <see cref="FakeLinearItemLinks"/>); the rest of the Linear rows wait in <see cref="LinearScenarios"/>.
/// ENG-12, ENG-31 and OPS-40 are assigned to the viewer Joi Example; ENG-57, ENG-44 and 50 more ENG issues (ENG-100 to ENG-149, newer than ENG-57) are Sam Lee's or nobody's.
/// 'teams-page2' asks the first page for its cursor and then page 2, so it holds the last two of 52 rows.
/// </summary>
internal static class LinearIssueScenarios
{
    private const string Viewer = "00000000-0000-4000-8000-0000000000a1";
    private const string Sam = "00000000-0000-4000-8000-0000000000a2";
    private const string Eng = "00000000-0000-4000-8000-0000000000e1";
    private const string Ops = "00000000-0000-4000-8000-0000000000e2";
    private const string Project = "00000000-0000-4000-8000-0000000000b1";
    private const string Cycle = "00000000-0000-4000-8000-0000000000c1";
    private const string Issues = "/sindri/linear/issues";

    private static readonly LinearPersonView _joi = new(Viewer, "Joi Example");
    private static readonly LinearPersonView _sam = new(Sam, "Sam Lee");
    private static readonly LinearLabelView _bug = new("00000000-0000-4000-8000-0000000000d1", "bug", "#eb5757");
    private static readonly LinearLabelView _api = new("00000000-0000-4000-8000-0000000000d2", "api", "#5e6ad2");
    private static readonly DateTimeOffset _synced = KnownInstants.Epoch.AddMinutes(-2);

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("linear-issues", "mine"), ("linear-issues", "teams-page2"), ("linear-issues", "filtered-empty"), ("linear-issues", "not-connected"),
        ("linear-issues", "feed-failing"), ("linear-issues", "rereading"), ("linear-issues", "session"),
        ("linear-issue", "full"), ("linear-issue", "removed"), ("linear-issue", "not-found"), ("linear-issue", "session"), ("linear-issue", "session-outside"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile over the scenario's stored issues, source and links.</summary>
    public static Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearIssueFacts>(slug == "linear-issue" ? OneFacts(scenario) : Facts(scenario)));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearItemLinks>(Links()));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearWriteGate>(scenario.StartsWith("session", StringComparison.Ordinal) ? new FakeLinearWriteGate().Off("SND-AUTH-002") : new FakeLinearWriteGate()));
        });

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        var caller = scenario.StartsWith("session", StringComparison.Ordinal) ? ItemScenarios.Session("demo") : ItemScenarios.Human(host);
        if (slug == "linear-issue")
        {
            var key = scenario switch { "removed" => "ENG-9", "not-found" => "ENG-99", "session-outside" => "OPS-40", _ => "ENG-12" };
            return (await host.Http.Ask(Get("/sindri/linear/issues/" + key, caller))).Body;
        }

        if (scenario == "teams-page2")
        {
            using var first = JsonDocument.Parse((await host.Http.Ask(Get(Issues + "?group=teams&limit=50", caller))).Body);
            return (await host.Http.Ask(Get(Issues + "?group=teams&limit=50&after=" + Uri.EscapeDataString(first.RootElement.GetProperty("next").GetString()!), caller))).Body;
        }

        var path = scenario switch
        {
            "filtered-empty" => Issues + "?group=mine&label=00000000-0000-4000-8000-0000000000ff",
            _ => Issues + "?group=mine",
        };
        return (await host.Http.Ask(Get(path, caller))).Body;
    }

    private static FakeLinearItemLinks Links() => new FakeLinearItemLinks()
        .With(Record(12), new LinearItemLinkView("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a301", "running"), new LinearPrView(2071, "https://github.com/acme/api/pull/2071", "waiting"))
        .With(Record(57), new LinearItemLinkView("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a302", "done"), new LinearPrView(2080, "https://github.com/acme/api/pull/2080", "approved"));

    private static FakeLinearIssueFacts Facts(string scenario)
    {
        var feed = scenario switch
        {
            "feed-failing" => new LinearFeedView("failing", At(31, 22, 5), "SND-LINEAR-010", Reread: null),
            "rereading" => new LinearFeedView("rereading", _synced, Code: null, new LinearRereadView(412, 1000)),
            "not-connected" => new LinearFeedView("ok", LastSyncedAt: null, Code: null, Reread: null),
            _ => new LinearFeedView("ok", _synced, Code: null, Reread: null),
        };
        var source = scenario == "not-connected" ? new LinearSourceFacts(Connected: false, Viewer: null, feed) : new LinearSourceFacts(Connected: true, _joi, feed);
        var facts = new FakeLinearIssueFacts().WithSource(source);
        _ = scenario switch
        {
            "not-connected" => facts,
            "rereading" => facts.WithIssues(Retry()),
            "session" => facts.VisibleTeams("ENG").WithIssues([.. Stored()]),
            _ => facts.WithIssues([.. Stored(), .. Fillers()]),
        };
        return facts;
    }

    // 'full' and 'session' are ENG-12 with its description and two comments; 'removed' is ENG-9, moved out of the profile's teams; the session reads only ENG; ENG-99 is never stored
    private static FakeLinearIssueFacts OneFacts(string scenario)
    {
        var facts = new FakeLinearIssueFacts().WithSource(new LinearSourceFacts(Connected: true, _joi, new LinearFeedView("ok", _synced, Code: null, Reread: null)));
        var comments = new LinearCommentView[]
        {
            new("00000000-0000-4000-8000-0000000000f2", _sam, At(31, 23, 30), "@Joi Example the budget should reset at midnight UTC, not on restart.", Sindri: false),
            new("00000000-0000-4000-8000-0000000000f1", _joi, At(31, 21, 0), "Started a run on this (ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a301).", Sindri: true),
        };
        const string Described = "The sync worker retries forever on a 500. Cap it at 5 tries per hour.\n\nSee https://example.com/runbooks/sync and the graph ![p95](https://example.com/p95.png).\nOld link: sindri://items/ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a301";
        var removed = Retry() with { Key = "ENG-9", Record = Record(9), Url = "https://linear.app/acme/issue/ENG-9", Title = "Old onboarding checklist", Project = null, Cycle = null, Labels = [] };
        _ = facts.VisibleTeams("ENG");
        return scenario.StartsWith("session", StringComparison.Ordinal)
            ? facts.WithDetails(new LinearIssueDetailFacts(Retry(), Described, Removed: false, comments), new LinearIssueDetailFacts(Rotate(), "Rotate it.", Removed: false, []))
            : facts.VisibleTeams("ENG", "OPS").WithDetails(new LinearIssueDetailFacts(Retry(), Described, Removed: false, comments), new LinearIssueDetailFacts(removed, "Moved to the OPS team.", Removed: true, []));
    }

    private static IEnumerable<LinearIssueRowView> Stored() => [Retry(), Export(), Rotate(), Queue(), Flaky()];

    private static string Record(int number) => "linear:issue/00000000-0000-4000-8000-" + (0x100 + number).ToString("x12", CultureInfo.InvariantCulture);

    private static DateTimeOffset At(int day, int hour, int minute) => new(2025, 12, day, hour, minute, 0, TimeSpan.Zero);

    private static LinearIssueRowView Retry() => new("ENG-12", Record(12), "https://linear.app/acme/issue/ENG-12", "Retry budget for the sync worker", new(Eng, "ENG"),
        new("00000000-0000-4000-8000-000000000052", "In Progress", "started"), 1, _joi, new(Project, "Sync worker"), new(Cycle, 14, "Cycle 14"), [_bug, _api], At(31, 23, 40), Link: null, Pr: null);

    private static LinearIssueRowView Export() => new("ENG-31", Record(31), "https://linear.app/acme/issue/ENG-31", "Export limits: cap the CSV at 50k rows", new(Eng, "ENG"),
        new("00000000-0000-4000-8000-000000000051", "Todo", "unstarted"), 2, _joi, new(Project, "Sync worker"), new(Cycle, 14, "Cycle 14"), [_api], At(31, 22, 10), Link: null, Pr: null);

    private static LinearIssueRowView Rotate() => new("OPS-40", Record(40), "https://linear.app/acme/issue/OPS-40", "Rotate the staging deploy key", new(Ops, "OPS"),
        new("00000000-0000-4000-8000-000000000054", "Backlog", "backlog"), 3, _joi, Project: null, Cycle: null, [], At(30, 9, 0), Link: null, Pr: null);

    private static LinearIssueRowView Queue() => new("ENG-57", Record(57), "https://linear.app/acme/issue/ENG-57", "Queue sizing: measure the p95 wait", new(Eng, "ENG"),
        new("00000000-0000-4000-8000-000000000053", "In Review", "started"), 2, _sam, new(Project, "Sync worker"), new(Cycle, 14, "Cycle 14"), [], At(31, 23, 55), Link: null, Pr: null);

    private static LinearIssueRowView Flaky() => new("ENG-44", Record(44), "https://linear.app/acme/issue/ENG-44", "Flaky login test on CI", new(Eng, "ENG"),
        new("00000000-0000-4000-8000-000000000051", "Todo", "unstarted"), 0, Assignee: null, Project: null, Cycle: null, [_bug], At(31, 18, 0), Link: null, Pr: null);

    // why: 50 more open ENG issues of Sam Lee's, all newer than ENG-57, so the first page of 'teams' is exactly these and page 2 starts at ENG-57
    private static IEnumerable<LinearIssueRowView> Fillers() => Enumerable.Range(100, 50).Select(n => new LinearIssueRowView(
        "ENG-" + n.ToString(CultureInfo.InvariantCulture), Record(n), "https://linear.app/acme/issue/ENG-" + n.ToString(CultureInfo.InvariantCulture), "Filler issue " + n.ToString(CultureInfo.InvariantCulture), new(Eng, "ENG"),
        new("00000000-0000-4000-8000-000000000052", "In Progress", "started"), 3, _sam, Project: null, Cycle: null, [], new DateTimeOffset(2025, 12, 31, 23, 59, 0, TimeSpan.Zero).AddSeconds(-(n - 100)), Link: null, Pr: null));
}
