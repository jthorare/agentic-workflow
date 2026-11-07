using Aw.Context.Core;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Settings, Connections, Linear (#2077 Ph3 1b; Linear hub spec 2026-11-04 §3; contract AGREED by api and ui): 'GET /linear/connection', 'POST /linear/connection/teams', 'POST /linear/connection' and
/// 'DELETE /linear/connection/write'. The ports behind them are fakes seeded per scenario (<see cref="FakeLinearConnectionFacts"/>, <see cref="FakeLinearKeyChecks"/>, <see cref="FakeLinearConnectionStore"/>,
/// <see cref="FakeLinearConnectionApproval"/>): nothing reaches Linear, a keychain or the profile file, so the profile hash is the item profile's. The state was last checked at 09:58, two minutes before the clock.
/// </summary>
internal static class LinearConnectionScenarios
{
    private const string ReadKey = "lin_api_read";
    private const string WriteKey = "lin_api_write";
    private static readonly DateTimeOffset _checked = KnownInstants.Epoch.AddMinutes(-2);

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("linear-connection", "connected"), ("linear-connection", "not-connected"), ("linear-connection", "read-only"), ("linear-connection", "scope-unknown"),
        ("linear-connection", "pending"), ("linear-connection", "session-refused"),
        ("linear-connection-teams", "two-teams"), ("linear-connection-teams", "refused"), ("linear-connection-teams", "unreachable"),
        ("linear-connect", "read-saved-approved"), ("linear-connect", "read-saved-waiting"), ("linear-connect", "write-saved"), ("linear-connect", "write-scope-unknown"),
        ("linear-connect", "same-as-read"), ("linear-connect", "no-keychain"), ("linear-connect", "no-teams"), ("linear-connect", "session-refused"),
        ("linear-disconnect-write", "removed"), ("linear-disconnect-write", "keychain-failed"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host: the item profile over the scenario's fakes.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearConnectionFacts>(Facts(scenario)));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearKeyChecks>(Checks(scenario)));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearConnectionStore>(Store(scenario)));
            _ = services.Replace(ServiceDescriptor.Singleton<ILinearConnectionApproval>(new FakeLinearConnectionApproval().Settling(scenario != "read-saved-waiting")));
        });

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);
        var caller = scenario.StartsWith("session", StringComparison.Ordinal) ? ItemScenarios.Session("demo") : ItemScenarios.Human(host);
        var request = slug switch
        {
            "linear-connection" => Get("/sindri/linear/connection", caller),
            "linear-connection-teams" => Post("/sindri/linear/connection/teams", $$"""{"key":"{{ReadKey}}"}""", caller),
            "linear-disconnect-write" => Delete("/sindri/linear/connection/write", caller),
            _ => Post("/sindri/linear/connection", Save(scenario), caller),
        };
        return (await host.Http.Ask(request)).Body;
    }

    private static string Save(string scenario) => scenario switch
    {
        "write-saved" or "write-scope-unknown" or "no-keychain" => $$"""{"kind":"write","key":"{{WriteKey}}"}""",
        "same-as-read" => $$"""{"kind":"write","key":"{{ReadKey}}"}""",
        "no-teams" => $$"""{"kind":"read","key":"{{ReadKey}}","teams":[]}""",
        _ => $$"""{"kind":"read","key":"{{ReadKey}}","teams":["ENG","OPS"]}""",
    };

    // 'connected' has both keys resolved at 09:58; 'pending' has a live profile that differs from the approved one; the others are one key short or unsure of the write key's scope
    private static FakeLinearConnectionFacts Facts(string scenario)
    {
        var resolves = new LinearKeyStateView("resolves", _checked);
        var unset = new LinearKeyStateView("notSet", CheckedAt: null);
        LinearConnectionTeamView[] teams = [new("00000000-0000-4000-8000-0000000000e1", "ENG", "Engineering"), new("00000000-0000-4000-8000-0000000000e2", "OPS", "Operations")];
        var view = scenario switch
        {
            "not-connected" => new LinearConnectionView(unset, unset, [], Pending: false),
            "read-only" => new LinearConnectionView(resolves, unset, teams, Pending: false),
            "pending" => new LinearConnectionView(resolves, unset, teams, Pending: true),
            "scope-unknown" => new LinearConnectionView(resolves, new LinearKeyStateView("scopeUnknown", _checked), teams, Pending: false),
            _ => new LinearConnectionView(resolves, resolves, teams, Pending: false),
        };
        return new FakeLinearConnectionFacts().Answering(view);
    }

    private static FakeLinearKeyChecks Checks(string scenario)
    {
        var checks = new FakeLinearKeyChecks().Reading(new LinearConnectionTeamView("00000000-0000-4000-8000-0000000000e1", "ENG", "Engineering"), new LinearConnectionTeamView("00000000-0000-4000-8000-0000000000e2", "OPS", "Operations"));
        return scenario switch
        {
            "refused" => checks.Failing(ContextErrors.LinearKeyRefused.With("Linear refused the key")),
            "unreachable" => checks.Failing(ContextErrors.SourceUnavailable.With("Linear did not answer")),
            "write-scope-unknown" => checks.WithScope(LinearKeyScope.Unknown),
            _ => checks,
        };
    }

    private static FakeLinearConnectionStore Store(string scenario) => scenario switch
    {
        "same-as-read" => new FakeLinearConnectionStore().WithReadKey(ReadKey),
        "no-keychain" => new FakeLinearConnectionStore().Failing(SindriErrors.LinearNoKeychain.With("No keychain to keep the write key in (fix: unlock your login keyring, or put the key in 1Password and set writeToken to op://…)")),
        "keychain-failed" => new FakeLinearConnectionStore().Failing(SindriErrors.LinearNoKeychain.With("Couldn't remove the write key from your keychain (the OS keychain would not delete the key); nothing changed (fix: unlock your keychain and press Remove again)")),
        _ => new FakeLinearConnectionStore(),
    };
}
