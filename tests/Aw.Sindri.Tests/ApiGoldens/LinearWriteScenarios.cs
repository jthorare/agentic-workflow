using Aw.Context.Core.Linear;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Linear;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Features.LinearIssues;
using CommentView = Aw.Sindri.Features.LinearIssues.LinearCommentView;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The Linear hub's writes (#2077 Ph3 1b; Linear hub spec 2026-11-04 §5.2, §5.3; contract ~/.agentic-workflow/digests/api-scripts/contract-2077-1b-linear-writes.md): 'POST /linear/issues/{key}/comments' and 'POST /linear/issues'.
/// Context's write facade is a <see cref="FakeContextLinearWrites"/> scripted per scenario, the team's lookups are ENG's from <see cref="LinearReadScenarios"/>, and the stored issues the route re-reads afterwards are a
/// <see cref="FakeLinearIssueFacts"/>. The edit rows ('linear-change') join this group from their own branch. 'linear-comment/applied-slow' is a write that only Context's re-read found (the fake's NextFoundOnReread).
/// </summary>
internal static class LinearWriteScenarios
{
    private const string ClientId = "5d0a4f6e-3b1c-4c0e-9a57-2f1d8c9b7e01";
    private const string Joi = "00000000-0000-4000-8000-0000000000a1";
    private const string Eng = "00000000-0000-4000-8000-0000000000e1";
    private const string Todo = "00000000-0000-4000-8000-000000000051";
    private const string Comment = "Budget resets at midnight UTC: agreed.";

    private static readonly DateTimeOffset _at = new(2026, 1, 1, 10, 5, 0, TimeSpan.Zero);

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("linear-comment", "applied"), ("linear-comment", "applied-slow"), ("linear-comment", "unknown"), ("linear-comment", "session-refused"),
        ("linear-create", "applied"), ("linear-create", "unknown"), ("linear-create", "bad-value"), ("linear-create", "session-refused"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile over the fakes of the write path.</summary>
    public static Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services => Wire(services, Writes(slug, scenario)));

    /// <summary>Replaces the write path's ports: Context's writes, the team's lookups, the stored issues and the write gate.</summary>
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
    internal static HttpRequestMessage Request(string slug, string scenario, (string Name, string Value)[] caller) => slug == "linear-comment"
        ? Post("/sindri/linear/issues/ENG-12/comments", $$"""{"clientId":"{{ClientId}}","body":"{{Comment}}"}""", caller)
        : Post("/sindri/linear/issues", scenario == "bad-value"
            ? $$"""{"clientId":"{{ClientId}}","team":"ENG","title":"Document the retry budget","assigneeId":"00000000-0000-4000-8000-0000000000ff"}"""
            : $$"""{"clientId":"{{ClientId}}","team":"ENG","title":"Document the retry budget"}""", caller);

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
}
