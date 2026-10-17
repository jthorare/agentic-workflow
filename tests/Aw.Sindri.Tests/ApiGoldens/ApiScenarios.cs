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
    /// <summary>Every (route slug, scenario) pair, in the order of the plan's golden list.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("status", "running-unapproved"), ("status", "running-approved"), ("status", "human-no-token"),
        ("status", "human-stale-token"), ("status", "unknown-caller"), ("status", "prover-refused"),
        ("ping", "running"), ("commands", "catalog"), ("events", "process-started"), ("events-catalog", "catalog"),
        ("stop", "accepted"), ("stop", "refused-session"), ("stop", "refused-origin"),
        ("profile-approve", "preview"), ("profile-approve", "approved"), ("profile-approve", "already-approved"),
        ("profile-approve", "refused-session"), ("profile-approve", "refused-no-terminal"), ("profile-approve", "refused-origin"),
        ("profile-approve", "stale-hash"), ("profile-approve", "bad-body"),
        ("workflow-recipe", "not-found"),
        .. ContextApiScenarios.Rows,
        .. ViewScenarios.All,
        .. ItemScenarios.All,
        .. ParkScenarios.All,
        .. RequestScenarios.All,
        .. StreamScenarios.All,
        .. JobScenarios.All,
        .. JobScopeScenarios.All,
        .. LaunchScenarios.All,
        .. SessionScenarios.All,
    ];

    /// <summary>Whether the scenario's host must refuse the prover label (its home isn't temporary).</summary>
    public static bool NeedsNonTemporaryHome(string slug, string scenario) => (slug, scenario) is ("status", "prover-refused");

    /// <summary>Starts the host the scenario runs on: the plain golden host, or for the context routes one over a small git repo with a map.</summary>
    public static async Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct) =>
        ContextApiScenarios.Handles(slug)
            ? await ContextApiScenarios.Start(slug, scenario, ct)
            : JobScenarios.Handles(slug, scenario) || JobScopeScenarios.Handles(slug, scenario)
            ? await Jobs.JobHost.Start(ct, pump: false)
            : LaunchScenarios.Handles(slug, scenario)
            ? await LaunchScenarios.Start(scenario, ct)
            : SessionScenarios.Handles(slug, scenario)
            ? await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml)
            : await GoldenHost.Start(temporary: !NeedsNonTemporaryHome(slug, scenario), ct, ProfileYaml(slug, scenario));

    /// <summary>The live profile the scenario's host starts with: the item scenarios need two repos.</summary>
    public static string ProfileYaml(string slug, string scenario) => ItemScenarios.Handles(slug, scenario) || ParkScenarios.Handles(slug, scenario) || RequestScenarios.Handles(slug, scenario) || StreamScenarios.Handles(slug, scenario) ? ItemScenarios.ProfileYaml : GoldenHost.ProfileYaml;

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        if (ContextApiScenarios.Handles(slug))
        {
            return await ContextApiScenarios.Body(host, slug, scenario, ct);
        }

        var token = host.Token.Value;
        if (Delegated(host, slug, scenario, ct) is { } delegated)
        {
            return await delegated();
        }

        return (slug, scenario) switch
        {
            ("status", "running-unapproved") => await Ask(host, Get("/sindri/status", Human(token))),
            ("status", "running-approved") => await AfterApproval(host, () => Get("/sindri/status", Human(token)), ct),
            ("status", "human-no-token") => await Ask(host, Get("/sindri/status", Human(token: null))),
            ("status", "human-stale-token") => await Ask(host, Get("/sindri/status", Human("token-of-an-earlier-start"))),
            ("status", "unknown-caller") => await Ask(host, Get("/sindri/status", (SindriHeaders.Caller, "admin"))),
            ("status", "prover-refused") => await Ask(host, Get("/sindri/status", Human(token, client: "prover"))),
            ("ping", "running") => await Ask(host, Get("/sindri/ping", Human(token))),
            ("commands", "catalog") => await Ask(host, Get("/sindri/commands", Human(token))),
            ("events", "process-started") => await FirstEventData(host, ct),
            ("events-catalog", "catalog") => await Ask(host, Get("/sindri/events/catalog", Human(token))),
            ("workflow-recipe", "not-found") => await Ask(host, Get(UnknownRecipe, Human(token))),
            ("stop", "accepted") => await Ask(host, Post("/sindri/stop", json: null, Human(token))),
            ("stop", "refused-session") => await Ask(host, Post("/sindri/stop", json: null, Session)),
            ("stop", "refused-origin") => await Ask(host, Post("/sindri/stop", json: null, [.. Human(token), Origin])),
            _ => await ApproveScenario(host, scenario, ct),
        };
    }

    /// <summary>The scenario's body when one of the scenario classes owns it, else null.</summary>
    private static Func<Task<string>>? Delegated(GoldenHost host, string slug, string scenario, CancellationToken ct) =>
        ItemScenarios.Handles(slug, scenario) ? () => ItemScenarios.Body(host, slug, scenario, ct)
        : ParkScenarios.Handles(slug, scenario) ? () => ParkScenarios.Body(host, slug, scenario, ct)
        : RequestScenarios.Handles(slug, scenario) ? () => RequestScenarios.Body(host, slug, scenario, ct)
        : StreamScenarios.Handles(slug, scenario) ? () => StreamScenarios.Body(host, slug, scenario, ct)
        : ViewScenarios.Handles(slug, scenario) ? () => ViewScenarios.Body(host, slug, scenario, ct)
        : JobScenarios.Handles(slug, scenario) ? () => JobScenarios.Body(host, slug, scenario, ct)
        : JobScopeScenarios.Handles(slug, scenario) ? () => JobScopeScenarios.Body(host, slug, scenario, ct)
        : LaunchScenarios.Handles(slug, scenario) ? () => LaunchScenarios.Body(host, slug, scenario, ct)
        : SessionScenarios.Handles(slug, scenario) ? () => SessionScenarios.Body(host, slug, scenario, ct)
        : null;

    private static (string Name, string Value)[] Session => [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")];

    // An unknown recipe answers the engine's SND-RECIPE-002 (WorkflowErrors.RecipeNotFound; API spec §3.8), from GET /workflow/recipes/{id} (API P11).
    private static string UnknownRecipe => "/sindri/workflow/recipes/workflow.unknown";

    private static (string Name, string Value) Origin => ("Origin", "http://localhost:3000");

    private static async Task<string> ApproveScenario(GoldenHost host, string scenario, CancellationToken ct)
    {
        var token = host.Token.Value;
        var hash = (await host.LiveHash(ct)).Value;
        var approve = $"{{\"hash\":\"{hash}\"}}";
        return scenario switch
        {
            "preview" => await Ask(host, Post("/sindri/profile/approve", "{\"hash\":null}", Human(token))),
            "approved" => await Ask(host, Post("/sindri/profile/approve", await Confirmed(host, hash), Human(token))),
            "already-approved" => await AfterApproval(host, () => Post("/sindri/profile/approve", approve, Human(token)), ct),
            "refused-session" => await Ask(host, Post("/sindri/profile/approve", approve, Session)),
            "refused-no-terminal" => await Ask(host, Post("/sindri/profile/approve", approve,
                [(SindriHeaders.Caller, "human"), (SindriHeaders.Client, "cli"), (SindriHeaders.Token, token)])),
            "refused-origin" => await Ask(host, Post("/sindri/profile/approve", approve, [.. Human(token), Origin])),
            "stale-hash" => await Ask(host, Post("/sindri/profile/approve",
                $"{{\"hash\":\"{SliceHarness.Profile("9s").Hash.Value}\"}}", Human(token))),
            "bad-body" => await Ask(host, Post("/sindri/profile/approve", "{\"hash\":", Human(token))),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such profile-approve scenario"),
        };
    }

    /// <summary>Approves the live profile as the human (checked), then sends the scenario's own request.</summary>
    private static async Task<string> AfterApproval(GoldenHost host, Func<HttpRequestMessage> request, CancellationToken ct)
    {
        var hash = (await host.LiveHash(ct)).Value;
        var setup = await host.Http.Ask(Post("/sindri/profile/approve", await Confirmed(host, hash), Human(host.Token.Value)));
        if (setup.Status != System.Net.HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"the setup approval failed: {setup.Status} {setup.Body}");
        }

        return await Ask(host, request());
    }

    /// <summary>An approve body for <paramref name="hash"/> that answers a fresh 'approve profile &lt;hash6&gt;' challenge as the human typed it (S1-D5).</summary>
    internal static async Task<string> Confirmed(GoldenHost host, string hash)
    {
        var action = "approve profile " + hash[..6];
        var issued = await host.Http.Ask(Post("/sindri/workflow/challenges", $"{{\"action\":\"{action}\"}}", Human(host.Token.Value)));
        if (issued.Status != System.Net.HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"issuing '{action}' failed: {issued.Status} {issued.Body}");
        }

        var challenge = System.Text.Json.JsonDocument.Parse(issued.Body).RootElement.GetProperty("challenge").GetString();
        return $"{{\"hash\":\"{hash}\",\"answer\":{{\"challenge\":\"{challenge}\",\"typed\":\"{action}\"}}}}";
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
