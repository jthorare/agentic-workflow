using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of 'POST /sindri/connections/{name}/signin' (connections PR 10b, #2349): a key-based connection refused with SND-CONN-011, and an oauth connection that needs a
/// sign-in accepted with its consent address. The store, the keychain, the preset and the browser sign-in are fakes seeded per scenario (the consent address is the fake's fixed one),
/// so nothing reaches a keychain, a provider or a browser.
/// </summary>
internal static class ConnectionSignInScenarios
{
    internal const string Slug = "connections-id-signin";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [(Slug, "key-based"), (Slug, "accepted")];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host over its seeded connection.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, extra: services =>
        {
            var store = new InMemoryConnectionStore();
            var secrets = new FakeConnectionSecrets();
            var row = Seeded(scenario);
            _ = secrets.Store(row.Name, new TokenSet("access-stored", "refresh-stored", ExpiresAt: null));
            _ = store.Upsert(row, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionStore>(store));
            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionSecrets>(secrets));
            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionPresetCatalog>(new FakeConnectionPresets(ConnectionHarness.Linear)));
            _ = services.Replace(ServiceDescriptor.Singleton<ISignInSession>(new FakeSignInSession()));
        });

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        await ViewScenarios.Approve(host, ct);
        var name = Uri.EscapeDataString(Seeded(scenario).Name.ToString());
        return (await host.Http.Ask(Post($"/sindri/connections/{name}/signin", json: null, Human(host.Token.Value)))).Body;
    }

    private static Connection Seeded(string scenario) => scenario == "key-based"
        ? ConnectionSamples.Oauth(ConnectionSamples.Name("acme", "key")) with { Kind = ConnectionKind.Rest, Preset = null, Config = new Dictionary<string, string>(StringComparer.Ordinal) }
        : ConnectionSamples.Oauth(status: ConnectionStatus.NeedsSignIn);
}
