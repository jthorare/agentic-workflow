using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P10a's goldens for the launch targets (UI spec E-3, screens spec E-28): 'GET /launch/targets' over a 'launch.probed' the scenario appends as the
/// launcher service would, with a spend reader of set figures for the budget ones, and 'POST /launch/targets/probe' (the Check again job).
/// </summary>
internal static class LaunchTargetScenarios
{
    private const string Budget = "agents:\n  claude-code: { dailyBudget: 10 }\n";
    private static readonly AgentId _claude = AgentId.From("claude-code");

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("launch-targets", "detected"), ("launch-targets", "file-exec-template"), ("launch-targets", "print"), ("launch-targets", "config-invalid"),
        ("launch-targets", "no-gui"), ("launch-targets", "budget-hard"), ("launch-targets", "budget-soft"), ("launch-targets", "no-usage-reader"),
        ("launch-targets", "override"), ("launch-targets", "not-probed"),
        ("launch-targets-probe", "started"), ("launch-targets-probe", "refused-session"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Whether the scenario sets a spend or a budget of its own, so it does not run on the plain launch host.</summary>
    /// <param name="scenario">The scenario.</param>
    /// <returns>True for the budget scenarios.</returns>
    public static bool NeedsOwnHost(string scenario) => string.Equals(scenario, "budget-hard", StringComparison.Ordinal) || string.Equals(scenario, "budget-soft", StringComparison.Ordinal) || string.Equals(scenario, "no-usage-reader", StringComparison.Ordinal);

    /// <summary>Starts the scenario's host: the budget ones read a spend of 10 (hard) or 8 (soft) for claude-code, 'no-usage-reader' has a budget and no figure.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct)
    {
        var spend = new FixedLaunchSpend();
        if (string.Equals(scenario, "budget-hard", StringComparison.Ordinal) || string.Equals(scenario, "budget-soft", StringComparison.Ordinal))
        {
            spend.Spent(_claude, string.Equals(scenario, "budget-hard", StringComparison.Ordinal) ? 10m : 8m);
        }

        var yaml = NeedsOwnHost(scenario) ? ItemScenarios.ProfileYaml + Budget : ItemScenarios.ProfileYaml;
        return GoldenHost.Start(temporary: true, ct, yaml, extra: services => services.Replace(ServiceDescriptor.Singleton<ILaunchSpend>(spend)));
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        var human = ItemScenarios.Human(host);
        if (slug == "launch-targets-probe")
        {
            return (await host.Http.Ask(Post("/sindri/launch/targets/probe", json: null, scenario == "started" ? human : ItemScenarios.Session("demo")))).Body;
        }

        if (scenario != "not-probed")
        {
            await ViewScenarios.Append(host, EventTypes.LaunchProbed, Probed(scenario), "launch.probed:0", ct);
            await ViewScenarios.CatchUp(host, ct);
        }

        return (await host.Http.Ask(Get("/sindri/launch/targets", human))).Body;
    }

    private static LaunchProbed Probed(string scenario) => new(Terminal(scenario), new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null), Targets(scenario),
        [new RepoFacts(RepoName.From("demo"), Present: true), new RepoFacts(RepoName.From("other"), Present: false)]);

    private static TerminalFacts Terminal(string scenario) => scenario switch
    {
        "file-exec-template" => new("exec-template", "kitty", Present: true, "file", "/home/demo/.agentic-workflow/launcher.json", Problem: null, Detail: null),
        "print" => new("print", "Print", Present: true, "detected", "/home/demo/.agentic-workflow/launcher.json", Problem: null, Detail: null),
        "config-invalid" => new("print", "Print", Present: false, "detected", "/home/demo/.agentic-workflow/launcher.json", "config-invalid", "line 4: viewer.argv must contain {attach}"),
        "no-gui" => new("exec-template", "xterm", Present: false, "detected", "/home/demo/.agentic-workflow/launcher.json", "no-gui-session", Detail: null),
        _ => new("exec-template", "xterm", Present: true, "detected", "/home/demo/.agentic-workflow/launcher.json", Problem: null, Detail: null),
    };

    private static TargetFacts[] Targets(string scenario) =>
    [
        new(_claude, Present: true, "1.0.0", Limited: null, Problem: null, CatalogSource.Builtin, Overridden: scenario == "override"),
        new(AgentId.From("codex"), Present: false, Version: null, Limited: null, "not-installed", CatalogSource.Builtin, Overridden: false),
        scenario == "override"
            ? new(AgentId.From("cursor"), Present: true, "0.9", "usage-unverified", Problem: null, CatalogSource.Override, Overridden: true)
            : new(AgentId.From("aider"), Present: true, "0.5", "no-usage-reader", Problem: null, CatalogSource.Added, Overridden: false),
    ];
}
