using System.Text.Json;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Linear;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.LinearReads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;
using CommentView = Aw.Sindri.Features.LinearIssues.LinearCommentView;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Linear hub's write routes (Linear hub spec 2026-11-04 §3, §5.2, §5.3, D6; Ph3 1b; contract ~/.agentic-workflow/digests/api-scripts/contract-2077-1b-linear-writes.md): 'POST /linear/issues/{key}/change',
/// 'POST /linear/issues/{key}/comments' and 'POST /linear/issues', each in its endings. Context's write facade is a <see cref="FakeContextLinearWrites"/> scripted per scenario.
/// The change rows run over ENG's five states, two members, two labels and one cycle (<c language="text">ChangeLookups</c>) and a stored ENG-12 already at the state the scenario ends in (In Review at 10:05 after an edit; Done at 10:01 for
/// the conflict); 'team-not-picked' reads ENG as a team the profile didn't pick (the gate is a fake, as in <see cref="LinearIssueScenarios"/>). The comment and create rows use ENG's lookups from <see cref="LinearReadScenarios"/> and
/// stored issues the route re-reads afterwards ('linear-comment/applied-slow' is a write that only Context's re-read found: the fake's NextFoundOnReread). One group, 'LinearWrite'.
/// </summary>
internal static class LinearWriteScenarios
{
    private const string ClientId = "5d0a4f6e-3b1c-4c0e-9a57-2f1d8c9b7e01";
    private const string Comment = "Budget resets at midnight UTC: agreed.";

    private const string Eng = "00000000-0000-4000-8000-0000000000e1";
    private const string Todo = "00000000-0000-4000-8000-000000000051";
    private const string Progress = "00000000-0000-4000-8000-000000000052";
    private const string Review = "00000000-0000-4000-8000-000000000053";
    private const string Done = "00000000-0000-4000-8000-000000000055";
    private const string Joi = "00000000-0000-4000-8000-0000000000a1";
    private const string Sam = "00000000-0000-4000-8000-0000000000a2";
    private const string Bug = "00000000-0000-4000-8000-0000000000d1";
    private const string Api = "00000000-0000-4000-8000-0000000000d2";
    private const string Project = "00000000-0000-4000-8000-0000000000b1";
    private const string Cycle = "00000000-0000-4000-8000-0000000000c1";

    private static readonly DateTimeOffset _synced = new(2025, 12, 31, 23, 58, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _at = new(2026, 1, 1, 10, 5, 0, TimeSpan.Zero);

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("linear-change", "applied"), ("linear-change", "applied-store-failed"), ("linear-change", "already-applied"), ("linear-change", "conflict"), ("linear-change", "unknown"),
        ("linear-change", "rate-limited"), ("linear-change", "no-write-key"), ("linear-change", "team-not-picked"), ("linear-change", "bad-value"), ("linear-change", "session-refused"),
        ("linear-comment", "applied"), ("linear-comment", "applied-slow"), ("linear-comment", "unknown"), ("linear-comment", "session-refused"),
        ("linear-create", "applied"), ("linear-create", "unknown"), ("linear-create", "bad-value"), ("linear-create", "session-refused"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile over the scenario's stored issue, the team's lookups, the write gate and a scripted Context.</summary>
    public static Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct) => GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services =>
    {
        if (slug == "linear-change")
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearIssueFacts>(ChangeStored(scenario)));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearItemLinks>(LinearIssueScenarios.Links()));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearWriteGate>(LinearIssueScenarios.Gate(scenario)));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearSideFacts>(new FakeLinearSideFacts().WithTeam(ChangeLookups()).WithTeamsPicked(picked: true)));
            _ = services.Replace(ServiceDescriptor.Singleton<IContextLinearWrites>(ChangeContext(scenario)));
        }
        else
        {
            Wire(services, Writes(slug, scenario));
        }
    });

    /// <summary>Replaces the comment and create write path's ports: Context's writes, the team's lookups, the stored issues and the write gate.</summary>
    internal static void Wire(IServiceCollection services, FakeContextLinearWrites writes)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.Replace(ServiceDescriptor.Singleton<IContextLinearWrites>(writes));
        _ = services.Replace(ServiceDescriptor.Singleton<ILinearSideFacts>(LinearReadScenarios.Facts("linear-lookups", "eng")));
        _ = services.Replace(ServiceDescriptor.Singleton<ILinearIssueFacts>(Stored()));
        _ = services.Replace(ServiceDescriptor.Singleton<ILinearItemLinks>(new FakeLinearItemLinks()));
        _ = services.Replace(ServiceDescriptor.Singleton<ILinearWriteGate>(new FakeLinearWriteGate()));
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        var caller = scenario.StartsWith("session", StringComparison.Ordinal) ? ItemScenarios.Session("demo") : ItemScenarios.Human(host);
        return (await host.Http.Ask(Request(slug, scenario, caller))).Body;
    }

    /// <summary>The request a scenario sends.</summary>
    internal static HttpRequestMessage Request(string slug, string scenario, (string Name, string Value)[] caller)
    {
        if (slug == "linear-change")
        {
            var changed = scenario == "bad-value" ? "00000000-0000-4000-8000-0000000000ff" : Review;
            return Post("/sindri/linear/issues/ENG-12/change", $$$"""{"opened":{"stateId":"{{{Progress}}}"},"set":{"stateId":"{{{changed}}}"}}""", caller);
        }

        return slug == "linear-comment"
            ? Post("/sindri/linear/issues/ENG-12/comments", $$"""{"clientId":"{{ClientId}}","body":"{{Comment}}"}""", caller)
            : Post("/sindri/linear/issues", scenario == "bad-value"
                ? $$"""{"clientId":"{{ClientId}}","team":"ENG","title":"Document the retry budget","assigneeId":"00000000-0000-4000-8000-0000000000ff"}"""
                : $$"""{"clientId":"{{ClientId}}","team":"ENG","title":"Document the retry budget"}""", caller);
    }

    // seeded by api: 'applied' of a create is Linear's answer ENG-58 in Todo; 'unknown' is a write with no answer and no sign of it on the re-read
    private static FakeContextLinearWrites Writes(string slug, string scenario)
    {
        var writes = new FakeContextLinearWrites();
        if (scenario == "unknown")
        {
            writes.Next(new LinearWriteUnknown());
        }
        else if ((slug, scenario) is ("linear-comment", "applied-slow"))
        {
            writes.NextFoundOnReread(Created());
        }
        else if ((slug, scenario) is ("linear-create", "applied"))
        {
            writes.Next(new LinearWriteApplied(Created()));
        }

        return writes;
    }

    private static LinearIssueView Created() => new(ExternalId.From("00000000-0000-4000-8000-00000000013a"), LinearIssueKey.Parse("ENG-58").Value, "ENG", "Document the retry budget", Description: null,
        ExternalId.From(Todo), "Todo", AssigneeId: null, Priority: 0, Estimate: null, CycleId: null, ProjectId: null, ParentId: null, [], _at);

    // ENG-12 holds the comment Sindri just posted (id ...f3, by the viewer), and ENG-58 is the issue Linear just made
    private static FakeLinearIssueFacts Stored()
    {
        var joi = new LinearPersonView(Joi, "Joi Example");
        var state = new LinearStateView(Todo, "Todo", "unstarted");
        var team = new LinearTeamView(Eng, "ENG");
        var retry = new LinearIssueRowView("ENG-12", "linear:issue/00000000-0000-4000-8000-00000000010c", "https://linear.app/acme/issue/ENG-12", "Retry budget for the sync worker", team, state, 1, joi, Project: null, Cycle: null, [], _at, Link: null, Pr: null);
        var made = new LinearIssueRowView("ENG-58", "linear:issue/00000000-0000-4000-8000-00000000013a", "https://linear.app/acme/issue/ENG-58", "Document the retry budget", team, state, 0, Assignee: null, Project: null, Cycle: null, [], _at, Link: null, Pr: null);
        return new FakeLinearIssueFacts()
            .WithSource(new LinearSourceFacts(Connected: true, joi, new LinearFeedView("ok", _at, Code: null, Reread: null)))
            .VisibleTeams("ENG")
            .WithDetails(
                new LinearIssueDetailFacts(retry, "The sync worker retries forever on a 500.", Removed: false, [new CommentView("00000000-0000-4000-8000-0000000000f3", joi, _at, Comment, Sindri: true)]),
                new LinearIssueDetailFacts(made, string.Empty, Removed: false, []));
    }

    // why: the stored issue is what the re-read serves, so it is already where the edit leaves it: In Review at 10:05, or Done at 10:01 when Linear moved it first
    private static FakeLinearIssueFacts ChangeStored(string scenario) => scenario == "conflict"
        ? LinearIssueScenarios.WriteFacts(new LinearStateView(Done, "Done", "completed"), new DateTimeOffset(2026, 1, 1, 10, 1, 0, TimeSpan.Zero))
        : LinearIssueScenarios.WriteFacts(new LinearStateView(Review, "In Review", "started"), new DateTimeOffset(2026, 1, 1, 10, 5, 0, TimeSpan.Zero));

    private static FakeContextLinearWrites ChangeContext(string scenario)
    {
        var writes = new FakeContextLinearWrites();
        var key = LinearIssueKey.Parse("ENG-12").Value;
        var live = Live(key, scenario switch { "conflict" => Done, "already-applied" => Review, _ => Progress }, scenario == "conflict" ? new DateTimeOffset(2026, 1, 1, 10, 1, 0, TimeSpan.Zero) : new DateTimeOffset(2025, 12, 31, 23, 40, 0, TimeSpan.Zero));
        writes.SetLive(live);
        var landed = live with { StateId = ExternalId.From(Review), StateName = "In Review", UpdatedAt = new DateTimeOffset(2026, 1, 1, 10, 5, 0, TimeSpan.Zero) };
        switch (scenario)
        {
            case "applied-store-failed":
                writes.Next(new LinearWriteAppliedNotStored(landed));
                break;
            case "unknown":
                writes.Next(new LinearWriteUnknown());
                break;
            case "rate-limited":
                writes.NextError(LinearHubErrors.RateLimited.With("Linear's rate limit: try again after 10:06 UTC. Nothing was changed.").WithExtension("retryAfter", JsonSerializer.SerializeToElement("2026-01-01T10:06:00.0000000Z")));
                break;
            default:
                break;
        }

        return writes;
    }

    private static LinearIssueView Live(LinearIssueKey key, string stateId, DateTimeOffset updatedAt) => new(
        ExternalId.From("00000000-0000-4000-8000-00000000010c"), key, "ENG", "Retry budget for the sync worker",
        "The sync worker retries forever on a 500. Cap it at 5 tries per hour.", ExternalId.From(stateId), stateId == Done ? "Done" : stateId == Review ? "In Review" : "In Progress",
        ExternalId.From(Joi), Priority: 1, Estimate: null, ExternalId.From(Cycle), ExternalId.From(Project), ParentId: null, [ExternalId.From(Bug), ExternalId.From(Api)], updatedAt);

    private static LinearLookupsResponse ChangeLookups() => new(
        new LinearTeamRef(Eng, "ENG", "Engineering"), _synced,
        [new("00000000-0000-4000-8000-000000000051", "Todo", "unstarted", 1), new(Progress, "In Progress", "started", 2), new(Review, "In Review", "started", 3), new("00000000-0000-4000-8000-000000000054", "Backlog", "backlog", 0), new(Done, "Done", "completed", 4)],
        [new(Joi, "Joi Example", Active: true), new(Sam, "Sam Lee", Active: true)],
        [new(Bug, "bug", "#eb5757"), new(Api, "api", "#5e6ad2")],
        [new(Cycle, 14, "Cycle 14", new DateTimeOffset(2025, 12, 22, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), Current: true)]);
}
