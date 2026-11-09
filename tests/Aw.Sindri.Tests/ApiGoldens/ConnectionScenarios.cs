using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of the connection routes the Connections page reads and writes (agent-first PR 18a): the list, the add (a key and an oauth sign-in that returns its consent address),
/// a refused add, one connection, an unknown one, the removal and the test. The store, the keychain, the presets, the probe and the sign-in are fakes seeded per scenario,
/// so nothing reaches a keychain, a provider or a browser. Every request is the owner's.
/// </summary>
internal static class ConnectionScenarios
{
    private const string Linear = "linear:work";
    private const string Acme = "acme:key";
    private const string ProbeUrl = "https://api.acme.example.test/ping";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("connections", "list"), ("connections", "added-key"), ("connections", "added-oauth"), ("connections", "refused"),
        ("connections-id", "one"), ("connections-id", "not-found"), ("connections-id", "removed"),
        ("connections-id-test", "ok"), ("connections-id-test", "failed"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host over its seeded connections.</summary>
    public static Task<GoldenHost> Start(string slug, string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, extra: services =>
        {
            var store = new InMemoryConnectionStore();
            var secrets = new FakeConnectionSecrets();
            foreach (var row in Seeded(slug, scenario))
            {
                var pointer = secrets.Store(row.Name, new TokenSet("access-stored", "refresh-stored", ExpiresAt: null)).Value;
                _ = store.Upsert(row with { SecretPointer = pointer }, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            }

            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionStore>(store));
            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionSecrets>(secrets));
            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionPresetCatalog>(new FakeConnectionPresets(ConnectionHarness.Linear)));
            _ = services.Replace(ServiceDescriptor.Singleton<ISignInSession>(new FakeSignInSession()));
            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionProbe>(new FakeConnectionProbe { Refuses = scenario == "failed" }));
        });

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        await ViewScenarios.Approve(host, ct);
        var owner = Human(host.Token.Value);
        var request = (slug, scenario) switch
        {
            ("connections", "list") => Get("/sindri/connections", owner),
            ("connections", "added-key") => Post("/sindri/connections", """{"name":"acme:key","preset":null,"kind":"rest","client":null,"keyEnv":null,"scopes":[],"wait":false,"key":"sk-golden-0001"}""", owner),
            ("connections", "added-oauth") => Post("/sindri/connections", """{"name":"linear:work","preset":"linear","kind":null,"client":"client-123","keyEnv":null,"scopes":[],"wait":false}""", owner),
            ("connections", "refused") => Post("/sindri/connections", """{"name":"linear:work","preset":"linear","kind":null,"client":null,"keyEnv":null,"scopes":[],"wait":false}""", owner),
            ("connections-id", "one") => Get($"/sindri/connections/{Uri.EscapeDataString(Linear)}", owner),
            ("connections-id", "not-found") => Get($"/sindri/connections/{Uri.EscapeDataString("acme:nobody")}", owner),
            ("connections-id", "removed") => Delete($"/sindri/connections/{Uri.EscapeDataString(Linear)}", owner),
            ("connections-id-test", _) => Post($"/sindri/connections/{Uri.EscapeDataString(Acme)}/test", json: null, owner),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such connection scenario"),
        };
        using (request)
        {
            return (await host.Http.Ask(request)).Body;
        }
    }

    private static List<Connection> Seeded(string slug, string scenario) => (slug, scenario) switch
    {
        ("connections", "list") => [Linear0(), Acme0(ConnectionStatus.Error)],
        ("connections-id", "one" or "removed") => [Linear0()],
        ("connections-id-test", _) => [Acme0(ConnectionStatus.NeedsSignIn)],
        _ => [],
    };

    private static Connection Linear0() => ConnectionSamples.Oauth() with { LastRefreshedAt = KnownInstants.Epoch.AddMinutes(30) };

    private static Connection Acme0(ConnectionStatus status) =>
        ConnectionSamples.Oauth(ConnectionSamples.Name("acme", "key"), status) with
        {
            Kind = ConnectionKind.Rest,
            Preset = null,
            Config = new Dictionary<string, string>(StringComparer.Ordinal) { ["probeUrl"] = ProbeUrl },
        };
}
