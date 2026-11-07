using System.Text.Json;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Linear;
using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Tests.LinearReads;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Linear hub's write routes (Linear hub spec 2026-11-04 §3, §5.3, D6; Ph3 1b; contract ~/.agentic-workflow/digests/api-scripts/contract-2077-1b-linear-writes.md): 'POST /linear/issues/{key}/change' in each ending.
/// Context is a scripted fake (<see cref="FakeContextLinearWrites"/>): its live read of ENG-12 is what Linear says, its next write is what Linear answers. The stored issue the re-read serves is a fake too, already at
/// the state the scenario ends in (In Review at 10:05 after an edit; Done at 10:01 for the conflict). ENG's lookups hold the five states 51 to 55, Joi Example and Sam Lee, two labels and one cycle. 'team-not-picked' reads
/// ENG as a team the profile didn't pick (the gate is a fake, as in <see cref="LinearIssueScenarios"/>). Rows are added to the group 'LinearWrite' by each write route.
/// </summary>
internal static class LinearWriteScenarios
{
    private const string Eng = "00000000-0000-4000-8000-0000000000e1";
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

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("linear-change", "applied"), ("linear-change", "applied-store-failed"), ("linear-change", "already-applied"), ("linear-change", "conflict"), ("linear-change", "unknown"),
        ("linear-change", "rate-limited"), ("linear-change", "no-write-key"), ("linear-change", "team-not-picked"), ("linear-change", "bad-value"), ("linear-change", "session-refused"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile over the scenario's stored issue, ENG's lookups, the write gate and a scripted Context.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct)
    {
        var writes = Context(scenario);
        return GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearIssueFacts>(Stored(scenario)));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearItemLinks>(LinearIssueScenarios.Links()));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearWriteGate>(LinearIssueScenarios.Gate(scenario)));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearSideFacts>(new FakeLinearSideFacts().WithTeam(Lookups()).WithTeamsPicked(picked: true)));
            _ = services.Replace(ServiceDescriptor.Singleton<IContextLinearWrites>(writes));
        });
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        var caller = scenario.StartsWith("session", StringComparison.Ordinal) ? ItemScenarios.Session("demo") : ItemScenarios.Human(host);
        var changed = scenario == "bad-value" ? "00000000-0000-4000-8000-0000000000ff" : Review;
        var request = Post("/sindri/linear/issues/ENG-12/change", $$$"""{"opened":{"stateId":"{{{Progress}}}"},"set":{"stateId":"{{{changed}}}"}}""", caller);
        return (await host.Http.Ask(request)).Body;
    }

    // why: the stored issue is what the re-read serves, so it is already where the edit leaves it: In Review at 10:05, or Done at 10:01 when Linear moved it first
    private static FakeLinearIssueFacts Stored(string scenario) => scenario == "conflict"
        ? LinearIssueScenarios.WriteFacts(new LinearStateView(Done, "Done", "completed"), new DateTimeOffset(2026, 1, 1, 10, 1, 0, TimeSpan.Zero))
        : LinearIssueScenarios.WriteFacts(new LinearStateView(Review, "In Review", "started"), new DateTimeOffset(2026, 1, 1, 10, 5, 0, TimeSpan.Zero));

    private static FakeContextLinearWrites Context(string scenario)
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

    private static LinearLookupsResponse Lookups() => new(
        new LinearTeamRef(Eng, "ENG", "Engineering"), _synced,
        [new("00000000-0000-4000-8000-000000000051", "Todo", "unstarted", 1), new(Progress, "In Progress", "started", 2), new(Review, "In Review", "started", 3), new("00000000-0000-4000-8000-000000000054", "Backlog", "backlog", 0), new(Done, "Done", "completed", 4)],
        [new(Joi, "Joi Example", Active: true), new(Sam, "Sam Lee", Active: true)],
        [new(Bug, "bug", "#eb5757"), new(Api, "api", "#5e6ad2")],
        [new(Cycle, 14, "Cycle 14", new DateTimeOffset(2025, 12, 22, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), Current: true)]);
}
