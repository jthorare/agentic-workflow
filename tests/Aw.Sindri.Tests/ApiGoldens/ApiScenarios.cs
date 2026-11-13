using Aw.Sindri.Host;
using Aw.Sindri.Tests.Callers;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Each API P1 route and scenario that has a golden file, as the request that produces its body on a fresh
/// <see cref="GoldenHost"/>. A scenario that needs earlier state (an approval before 'running-approved') makes those
/// requests itself, so every golden is independent of the others.
/// </summary>
internal static class ApiScenarios
{
    /// <summary>The rows of the routes with no scenario file of their own: status, ping, commands, events, stop and profile-approve.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> Core { get; } =
    [
        ("status", "running-unapproved"), ("status", "human-no-token"),
        ("status", "human-stale-token"), ("status", "unknown-caller"), ("status", "prover-refused"),
        ("ping", "running"), ("commands", "catalog"), ("events", "process-started"), ("events-catalog", "catalog"),
        ("stop", "accepted"), ("stop", "refused-origin"),
        ("profile-approve", "removed"),
    ];

    /// <summary>The rows by route group, each run by its own test class (the per-group ApiGoldenTests classes) so xUnit runs the groups in parallel; every row starts a host of its own, so no group shares state.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<(string Slug, string Scenario)>> Groups { get; } = new Dictionary<string, IReadOnlyList<(string Slug, string Scenario)>>(StringComparer.Ordinal)
    {
        ["Core"] = Core,
        ["Context"] = ContextApiScenarios.Rows,
        ["Views"] = ViewScenarios.All,
        ["Sessions"] = SessionScenarios.All,
        ["Agents"] = AgentScenarios.All,
        ["Items"] = [.. ItemScenarios.All],
        ["Requests"] = RequestScenarios.All,
        ["Streams"] = StreamScenarios.All,
        ["Jobs"] = [.. JobScenarios.All, .. JobScopeScenarios.All],
        ["Launches"] = [.. LaunchScenarios.All, .. WrapSessionScenarios.All],
        ["Setup"] = [.. SetupScenarios.All, .. SetupRepoScenarios.All],
        ["Recipes"] = AppChallengeScenarios.All,
        ["CodeIndex"] = CodeIndexScenarios.All,
        ["Sandbox"] = SandboxScenarios.All,
        ["Projects"] = ProjectScenarios.All,
        ["Todos"] = TodoScenarios.All,
        ["Skills"] = SkillScenarios.All,
        ["LinearIssues"] = LinearIssueScenarios.All,
        ["Reviews"] = [.. ReviewScenarios.All, .. ReviewLoginScenarios.All, .. ReviewFixScenarios.All, .. LibraryScenarios.All, .. LibraryDocScenarios.All],
        ["LinearReads"] = LinearReadScenarios.All,
        ["LinearWrite"] = LinearWriteScenarios.All,
        ["LinearConnection"] = LinearConnectionScenarios.All,
        ["ConnectionSignIn"] = ConnectionSignInScenarios.All,
        ["Connections"] = ConnectionScenarios.All,
        ["Memories"] = MemoryScenarios.All,
        ["WorkflowRuns"] = WorkflowRunScenarios.All,
        ["WorkflowReads"] = WorkflowReadScenarios.All,
        ["Resources"] = ResourceScenarios.All,
        ["AgentStarts"] = AgentStartScenarios.All,
        ["Toolkits"] = ToolkitScenarios.All,
        ["WorkflowWrites"] = WorkflowWriteScenarios.All,
    };

    /// <summary>Every (route slug, scenario) pair, group by group.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [.. Groups.Values.SelectMany(g => g)];

    /// <summary>Whether the scenario's host must refuse the prover label (its home isn't temporary).</summary>
    public static bool NeedsNonTemporaryHome(string slug, string scenario) => (slug, scenario) is ("status", "prover-refused");

    /// <summary>Starts the host the scenario runs on: the plain golden host, or for the context routes one over a small git repo with a map.</summary>
    public static async Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct) =>
        CodeIndexScenarios.Handles(slug, scenario)
            ? await CodeIndexScenarios.Start(ct, demoBehindMain: CodeIndexScenarios.DemoBehindMain(scenario))
            : ConnectionSignInScenarios.Handles(slug, scenario)
            ? await ConnectionSignInScenarios.Start(scenario, ct)
            : ConnectionScenarios.Handles(slug, scenario)
            ? await ConnectionScenarios.Start(slug, scenario, ct)
            : MemoryScenarios.Handles(slug, scenario)
            ? await MemoryScenarios.Start(scenario, ct)
            : ContextApiScenarios.Handles(slug)
            ? await ContextApiScenarios.Start(slug, scenario, ct)
            : (slug, scenario) is ("profile", "repo-added")
            ? await ViewScenarios.StartWithGit(ct)
            : IsSetup(slug, scenario)
            ? await SetupHost(slug, scenario, ct)
            : JobScenarios.Handles(slug, scenario) || JobScopeScenarios.Handles(slug, scenario)
            ? await Jobs.JobHost.Start(ct, pump: false)
            : await StartLaunchOrLater(slug, scenario, ct);

    /// <summary>The last hosts of <see cref="Start"/>: the answered workflow run, the launch, approve-all, item-scope and wrapped-session ones, else the plain golden host.</summary>
    private static async Task<GoldenHost> StartLaunchOrLater(string slug, string scenario, CancellationToken ct) =>
        WorkflowRunScenarios.NeedsHost(slug, scenario)
            ? await WorkflowRunScenarios.Start(ct)
            : IsSkillOrLinear(slug, scenario)
            ? await StartSkillOrLinear(slug, scenario, ct)
            : IsReviews(slug, scenario)
            ? await StartReviews(slug, scenario, ct)
            : IsProjectOrTodo(slug, scenario)
            ? await StartProjectOrTodo(slug, scenario, ct)
            : SandboxScenarios.Handles(slug, scenario)
            ? await SandboxScenarios.Start(slug, scenario, ct)
            : LaunchScenarios.Handles(slug, scenario)
            ? await LaunchScenarios.Start(scenario, ct)
            : AppChallengeScenarios.Handles(slug, scenario)
            ? await GoldenHost.Start(temporary: true, ct)
            : WrapSessionScenarios.Handles(slug, scenario)
            ? await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml)
            : SessionScenarios.NeedsCgroup(slug, scenario)
            ? await SessionScenarios.Start(ct)
            : await GoldenHost.Start(temporary: !NeedsNonTemporaryHome(slug, scenario), ct, ProfileYaml(slug, scenario));

    private static bool IsProjectOrTodo(string slug, string scenario) => ProjectScenarios.Handles(slug, scenario) || TodoScenarios.Handles(slug, scenario);

    /// <summary>The host of a Projects-group or Todos-group scenario.</summary>
    private static Task<GoldenHost> StartProjectOrTodo(string slug, string scenario, CancellationToken ct) =>
        TodoScenarios.Handles(slug, scenario) ? TodoScenarios.Start(slug, ct) : ProjectScenarios.Start(slug, scenario, ct);

    /// <summary>Whether the scenario belongs to the Linear hub's reads (the issue list and sheet, the lookups and the strip).</summary>
    private static bool IsLinear(string slug, string scenario) => LinearIssueScenarios.Handles(slug, scenario) || LinearReadScenarios.Handles(slug, scenario) || LinearWriteScenarios.Handles(slug, scenario) || LinearConnectionScenarios.Handles(slug, scenario);

    /// <summary>Whether the scenario belongs to the Skills group or the Linear hub's reads.</summary>
    private static bool IsSkillOrLinear(string slug, string scenario) => SkillScenarios.Handles(slug, scenario) || IsLinear(slug, scenario);

    /// <summary>The host of a Skills scenario, else of a Linear read scenario.</summary>
    private static Task<GoldenHost> StartSkillOrLinear(string slug, string scenario, CancellationToken ct) =>
        SkillScenarios.Handles(slug, scenario) ? SkillScenarios.Start(ct) : StartLinear(slug, scenario, ct);

    /// <summary>The host of a Linear read scenario.</summary>
    private static Task<GoldenHost> StartLinear(string slug, string scenario, CancellationToken ct) =>
        LinearIssueScenarios.Handles(slug, scenario) ? LinearIssueScenarios.Start(slug, scenario, ct)
        : LinearWriteScenarios.Handles(slug, scenario) ? LinearWriteScenarios.Start(slug, scenario, ct)
        : LinearConnectionScenarios.Handles(slug, scenario) ? LinearConnectionScenarios.Start(scenario, ct)
        : LinearReadScenarios.Start(slug, scenario, ct);

    /// <summary>Whether the scenario belongs to the Reviews group (the pages, the login save, the library and one doc of it).</summary>
    private static bool IsReviews(string slug, string scenario) =>
        ReviewScenarios.Handles(slug, scenario) || ReviewLoginScenarios.Handles(slug, scenario) || ReviewFixScenarios.Handles(slug, scenario) || LibraryScenarios.Handles(slug, scenario) || LibraryDocScenarios.Handles(slug, scenario);

    /// <summary>The host of a Reviews-group scenario.</summary>
    private static Task<GoldenHost> StartReviews(string slug, string scenario, CancellationToken ct) =>
        ReviewFixScenarios.Handles(slug, scenario) ? ReviewFixScenarios.Start(scenario, ct)
        : LibraryScenarios.Handles(slug, scenario) ? LibraryScenarios.Start(scenario, ct)
        : LibraryDocScenarios.Handles(slug, scenario) ? LibraryDocScenarios.Start(ct)
        : ReviewScenarios.Start(slug, scenario, ct);

    /// <summary>Whether the scenario is a setup one (the steps, or adding a repo).</summary>
    private static bool IsSetup(string slug, string scenario) =>
        SetupScenarios.Handles(slug, scenario) || SetupRepoScenarios.Handles(slug, scenario);

    /// <summary>The host of a setup scenario (the steps, or adding a repo).</summary>
    private static Task<GoldenHost> SetupHost(string slug, string scenario, CancellationToken ct) =>
        SetupScenarios.Handles(slug, scenario) ? SetupScenarios.Start(scenario, ct) : SetupRepoScenarios.Start(scenario, ct);

    /// <summary>The live profile the scenario's host starts with: the item scenarios need two repos.</summary>
    public static string ProfileYaml(string slug, string scenario) => SessionScenarios.Handles(slug, scenario) ? SessionScenarios.ProfileYaml : ItemScenarios.Handles(slug, scenario) || RequestScenarios.Handles(slug, scenario) || StreamScenarios.Handles(slug, scenario) ? ItemScenarios.ProfileYaml : GoldenHost.ProfileYaml;

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        if (CodeIndexScenarios.Handles(slug, scenario))
        {
            return await CodeIndexScenarios.Body(host, slug, scenario, ct);
        }

        if (ContextApiScenarios.Handles(slug))
        {
            return await ContextApiScenarios.Body(host, slug, scenario, ct);
        }

        var token = host.Token.Value;
        if (DelegatedOrWorkflow(host, slug, scenario, ct) is { } delegated)
        {
            return await delegated();
        }

        return (slug, scenario) switch
        {
            ("status", "running-unapproved") => await Ask(host, Get("/sindri/status", Human(token))),
            ("status", "human-no-token") => await Ask(host, Get("/sindri/status", Human(token: null))),
            ("status", "human-stale-token") => await Ask(host, Get("/sindri/status", Human("token-of-an-earlier-start"))),
            ("status", "unknown-caller") => await Ask(host, Get("/sindri/status", (SindriHeaders.Caller, "admin"))),
            ("status", "prover-refused") => await Ask(host, Get("/sindri/status", Human(token, client: "prover"))),
            ("ping", "running") => await Ask(host, Get("/sindri/ping", Human(token))),
            ("commands", "catalog") => await Ask(host, Get("/sindri/commands", Human(token))),
            ("events", "process-started") => await FirstEventData(host, ct),
            ("events-catalog", "catalog") => await Ask(host, Get("/sindri/events/catalog", Human(token))),
            ("stop", "accepted") => await Ask(host, Post("/sindri/stop", json: null, Human(token))),
            ("stop", "refused-origin") => await Ask(host, Post("/sindri/stop", json: null, [.. Human(token), Origin])),
            _ => await ApproveScenario(host, scenario, ct),
        };
    }

    /// <summary>The scenario's body when the workflow-run class owns it, else <see cref="Delegated"/>'s.</summary>
    private static Func<Task<string>>? DelegatedOrWorkflow(GoldenHost host, string slug, string scenario, CancellationToken ct) =>
        WorkflowRunScenarios.Handles(slug, scenario) ? () => WorkflowRunScenarios.Body(host, slug, scenario, ct)
        : WorkflowReadScenarios.Handles(slug, scenario) ? () => WorkflowReadScenarios.Body(host, slug, scenario, ct)
        : AgentStartScenarios.Handles(slug, scenario) ? () => AgentStartScenarios.Body(host, slug, scenario)
        : ResourceScenarios.Handles(slug, scenario) ? () => ResourceScenarios.Body(host, slug, scenario, ct)
        : ToolkitScenarios.Handles(slug, scenario) ? () => ToolkitScenarios.Body(host, slug, scenario, ct)
        : AgentScenarios.Handles(slug, scenario) ? () => AgentScenarios.Body(host, scenario, ct)
        : WorkflowWriteScenarios.Handles(slug, scenario) ? () => WorkflowWriteScenarios.Body(host, slug, scenario, ct)
        : Delegated(host, slug, scenario, ct);

    /// <summary>The scenario's body when one of the scenario classes owns it, else null.</summary>
    private static Func<Task<string>>? Delegated(GoldenHost host, string slug, string scenario, CancellationToken ct) =>
        ItemScenarios.Handles(slug, scenario) ? () => ItemScenarios.Body(host, slug, scenario, ct)
        : RequestScenarios.Handles(slug, scenario) ? () => RequestScenarios.Body(host, slug, scenario, ct)
        : StreamScenarios.Handles(slug, scenario) ? () => StreamScenarios.Body(host, slug, scenario, ct)
        : ViewScenarios.Handles(slug, scenario) ? () => ViewScenarios.Body(host, slug, scenario, ct)
        : SetupScenarios.Handles(slug, scenario) ? () => SetupScenarios.Body(host, slug, scenario, ct)
        : JobScenarios.Handles(slug, scenario) ? () => JobScenarios.Body(host, slug, scenario, ct)
        : JobScopeScenarios.Handles(slug, scenario) ? () => JobScopeScenarios.Body(host, slug, scenario, ct)
        : LaunchScenarios.Handles(slug, scenario) ? () => LaunchScenarios.Body(host, slug, scenario, ct)
        : SessionBody(host, slug, scenario, ct);

    /// <summary>The body of an app-challenge (recipes group), collector (sessions) or wrap (launch) session scenario, else null.</summary>
    private static Func<Task<string>>? SessionBody(GoldenHost host, string slug, string scenario, CancellationToken ct) =>
        AppChallengeScenarios.Handles(slug, scenario) ? () => AppChallengeScenarios.Body(host, scenario, ct)
        : SetupRepoScenarios.Handles(slug, scenario) ? () => SetupRepoScenarios.Body(host, slug, scenario)
        : SessionScenarios.Handles(slug, scenario) ? () => SessionScenarios.Body(host, slug, scenario, ct)
        : WrapSessionScenarios.Handles(slug, scenario) ? () => WrapSessionScenarios.Body(host, slug, scenario, ct)
        : SandboxScenarios.Handles(slug, scenario) ? () => SandboxScenarios.Body(host, slug, scenario, ct)
        : ProjectScenarios.Handles(slug, scenario) ? () => ProjectScenarios.Body(host, slug, scenario, ct)
        : TodoScenarios.Handles(slug, scenario) ? () => TodoScenarios.Body(host, slug, scenario, ct)
        : SkillScenarios.Handles(slug, scenario) ? () => SkillScenarios.Body(host, ct)
        : LinearBody(host, slug, scenario, ct);

    /// <summary>The body of a Linear hub scenario (the issue list and sheet, the lookups and the strip, the write and connection routes), else of a Reviews-group one.</summary>
    private static Func<Task<string>>? LinearBody(GoldenHost host, string slug, string scenario, CancellationToken ct) =>
        LinearIssueScenarios.Handles(slug, scenario) ? () => LinearIssueScenarios.Body(host, slug, scenario, ct)
        : LinearReadScenarios.Handles(slug, scenario) ? () => LinearReadScenarios.Body(host, slug, scenario)
        : LinearWriteScenarios.Handles(slug, scenario) ? () => LinearWriteScenarios.Body(host, slug, scenario, ct)
        : LinearConnectionScenarios.Handles(slug, scenario) ? () => LinearConnectionScenarios.Body(host, slug, scenario, ct)
        : ReviewBody(host, slug, scenario, ct);

    /// <summary>The body of a Reviews-group scenario (the pages, the login save, the library), else null.</summary>
    private static Func<Task<string>>? ReviewBody(GoldenHost host, string slug, string scenario, CancellationToken ct) =>
        ReviewScenarios.Handles(slug, scenario) ? () => ReviewScenarios.Body(host, slug, scenario, ct)
        : ReviewLoginScenarios.Handles(slug, scenario) ? () => ReviewLoginScenarios.Body(host, scenario, ct)
        : ReviewFixScenarios.Handles(slug, scenario) ? () => ReviewFixScenarios.Body(host, scenario, ct)
        : LibraryScenarios.Handles(slug, scenario) ? () => LibraryScenarios.Body(host, scenario, ct)
        : LibraryDocScenarios.Handles(slug, scenario) ? () => LibraryDocScenarios.Body(host, scenario, ct)
        : ConnectionSignInScenarios.Handles(slug, scenario) ? () => ConnectionSignInScenarios.Body(host, scenario, ct)
        : ConnectionScenarios.Handles(slug, scenario) ? () => ConnectionScenarios.Body(host, slug, scenario, ct)
        : MemoryScenarios.Handles(slug, scenario) ? () => MemoryScenarios.Body(host, slug, scenario, ct)
        : null;


    // An unknown recipe answers the engine's SND-RECIPE-002 (WorkflowErrors.RecipeNotFound; API spec §3.8), from GET /workflow/recipes/{id} (API P11).

    private static (string Name, string Value) Origin => ("Origin", "http://localhost:3000");

    /// <summary>The removed route (#2258 PR 1): a human's POST gets 410 SND-API-014.</summary>
    private static async Task<string> ApproveScenario(GoldenHost host, string scenario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return scenario == "removed"
            ? await Ask(host, Post("/sindri/profile/approve", "{\"hash\":null}", Human(host.Token.Value)))
            : throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such profile-approve scenario");
    }

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;

    /// <summary>The first server-sent event's data line from GET /sindri/events (the ledger's first event).</summary>
    private static async Task<string> FirstEventData(GoldenHost host, CancellationToken ct)
    {
        using var request = Get("/sindri/events", Human(host.Token.Value));
        using var response = await host.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                return line["data: ".Length..];
            }
        }

        throw new InvalidOperationException("the event stream ended before its first event");
    }
}
