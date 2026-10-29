using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Features.GetSandboxStatus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// SB-6a's goldens: 'GET /sandbox/denials?item=' over an item's launch (and one launch of no item), and 'POST /sandbox/allow' for the host that can be allowed, the path that never can,
/// a tool, and a browser page; the counters are fixed (<see cref="FixedDenials"/>), the launch and the item are appended to the ledger as the launcher would. And the sandbox status and check
/// goldens (sandbox spec §8): the real checks probe this box, so the host runs them over a fixed answer (the machine's memory, the platform's mechanism and the blocker the check finds are the
/// scenario's), recorded the way serve records any check. 'blocked' finds bubblewrap missing; 'ready' finds nothing.
/// </summary>
internal static class SandboxScenarios
{
    private const string Owned = "LCH-00000000000000000000000000000001";
    private const string Unowned = "LCH-00000000000000000000000000000002";
    private const long Gib = 1024L * 1024 * 1024;

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("sandbox-denials", "by-item"), ("sandbox-denials", "other-item"),
        ("sandbox-allow", "needs-approval"), ("sandbox-allow", "refused-never"), ("sandbox-allow", "refused-policy"), ("sandbox-allow", "refused-origin"),
        ("sandbox-status", "never-checked"), ("sandbox-status", "after-blocked-check"), ("sandbox-status", "after-ready-check"),
        ("sandbox-check", "blocked"), ("sandbox-check", "ready"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the denial and allow scenarios over the item scenarios' profile with the fixed counters, the status and check ones over the fixed machine, platform and check.</summary>
    public static Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct)
    {
        if (!IsStatus(slug))
        {
            return GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services => services.Replace(ServiceDescriptor.Singleton<IDenialFiles>(new FixedDenials())));
        }

        var blocked = scenario.Contains("blocked", StringComparison.Ordinal);
        return GoldenHost.Start(temporary: true, ct, GoldenHost.ProfileYaml, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ISandboxMachine>(new FixedMachine()));
            _ = services.Replace(ServiceDescriptor.Singleton<ISandboxPlatformName>(new FixedPlatform()));
            _ = services.Replace(ServiceDescriptor.Scoped<ISandboxChecks>(sp => new RecordingSandboxChecks(new FixedChecks(blocked), sp.GetRequiredService<LastSandboxCheck>(), sp.GetRequiredService<TimeProvider>())));
        });
    }

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        if (IsStatus(slug))
        {
            return await StatusBody(host, slug, scenario, ct);
        }

        await ViewScenarios.Approve(host, ct);
        var item = await ItemScenarios.Create(host, "Try the sandbox", "demo");
        await LaunchScenarios.Started(host, LaunchId.From(Owned), item, ViewerOutcomeKind.Skipped, ct);
        await ViewScenarios.CatchUp(host, ct);
        var human = ItemScenarios.Human(host);
        return slug == "sandbox-denials"
            ? (await host.Http.Ask(Get(scenario == "by-item" ? $"/sindri/sandbox/denials?item={item}" : "/sindri/sandbox/denials?item=ITM-00000000000000000000000000000fff", human))).Body
            : (await host.Http.Ask(Post("/sindri/sandbox/allow", Allow(scenario), scenario == "refused-origin" ? [.. human, ("Origin", "http://localhost:3000")] : human))).Body;
    }

    /// <summary>Whether <paramref name="slug"/> is the status or the check route (else the denials or the allow one).</summary>
    private static bool IsStatus(string slug) => string.Equals(slug, "sandbox-status", StringComparison.Ordinal) || string.Equals(slug, "sandbox-check", StringComparison.Ordinal);

    /// <summary>The status and check scenarios: a check first for the 'after-' ones, then the status.</summary>
    private static async Task<string> StatusBody(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var headers = Human(host.Token.Value);
        if (slug == "sandbox-check")
        {
            return (await host.Http.Ask(Get("/sindri/sandbox/check", headers))).Body;
        }

        if (scenario.StartsWith("after-", StringComparison.Ordinal))
        {
            _ = await host.Http.Ask(Get("/sindri/sandbox/check", headers));
        }

        return (await host.Http.Ask(Get("/sindri/sandbox/status", headers))).Body;
    }

    private static string Allow(string scenario) => scenario switch
    {
        "refused-never" => $$"""{"launch":"{{Owned}}","kind":"path","target":"/var/run/docker.sock"}""",
        "refused-policy" => $$"""{"launch":"{{Owned}}","kind":"policy","target":"git push"}""",
        _ => $$"""{"launch":"{{Owned}}","kind":"network","target":"github.com"}""",
    };

    /// <summary>The counters of two launches: the first is the item's, the second no item's.</summary>
    private sealed class FixedDenials : IDenialFiles
    {
        private static readonly DateTimeOffset _at = KnownInstants.Epoch;

        public ValueTask<IReadOnlyList<DenialFile>> List(LaunchId? launch, CancellationToken cancellationToken)
        {
            IReadOnlyList<DenialFile> all =
            [
                new(
                    LaunchId.From(Owned), _at,
                    [
                        new(DenialKind.Network, "github.com", DenialRules.NoGroup, 3, _at, _at),
                        new(DenialKind.Path, "/var/run/docker.sock", DenialRules.DenyList, 1, _at, _at),
                        new(DenialKind.Policy, "git push", DenialRules.InnerPolicy, 1, _at, _at),
                    ], []),
                new(LaunchId.From(Unowned), _at, [new(DenialKind.Network, "example.org", DenialRules.NoGroup, 1, _at, _at)], []),
            ];
            return ValueTask.FromResult<IReadOnlyList<DenialFile>>([.. all.Where(f => launch is not { } one || f.LaunchId == one)]);
        }
    }

    private sealed class FixedMachine : ISandboxMachine
    {
        public long RamBytes => 62 * Gib;

        public int Cores => 8;
    }

    private sealed class FixedPlatform : ISandboxPlatformName
    {
        public string Mechanism => "bubblewrap";
    }

    private sealed class FixedChecks(bool blocked) : ISandboxChecks
    {
        public ValueTask<SandboxCheckReport> Run(CancellationToken cancellationToken) => ValueTask.FromResult(new SandboxCheckReport(
            blocked ? [SandboxBlocker.Of(SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoBwrap))] : [], []));
    }
}
