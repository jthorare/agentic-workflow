using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Connections;

/// <summary>#2369: a keychain read can take seconds (a locked keychain, a prompt), so 'connection signin' must not hold the command gate while it reads the secret.</summary>
public sealed class SignInGateTests
{
    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Signin_reads_the_keychain_without_holding_the_command_gate()
    {
        var ct = TestContext.Current.CancellationToken;
        var row = ConnectionSamples.Oauth(status: ConnectionStatus.NeedsSignIn);
        var reads = new List<bool>();
        await using var host = await GoldenHost.Start(temporary: true, ct, extra: services =>
        {
            var store = new InMemoryConnectionStore();
            _ = store.Upsert(row, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionStore>(store));
            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionSecrets>(sp => new GateProbingSecrets(sp.GetRequiredService<CommandGateLock>(), reads)));
            _ = services.Replace(ServiceDescriptor.Singleton<IConnectionPresetCatalog>(new FakeConnectionPresets(ConnectionHarness.Linear)));
            _ = services.Replace(ServiceDescriptor.Singleton<ISignInSession>(new FakeSignInSession()));
        });
        await ViewScenarios.Approve(host, ct);

        var answer = await host.Http.Ask(Post("/sindri/connections/" + Uri.EscapeDataString(row.Name.ToString()) + "/signin", json: null, Human(host.Token.Value)));

        Assert.Contains("consentUrl", answer.Body, StringComparison.Ordinal);
        Assert.NotEmpty(reads);   // the sign-in did read the stored client secret
        Assert.DoesNotContain(true, reads);   // and none of those reads happened while the gate was held
    }

    // The fake keychain, which notes for every read whether the command gate was taken at that moment: the gate is free when a short wait for it succeeds at once.
    private sealed class GateProbingSecrets(CommandGateLock gate, List<bool> reads) : IConnectionSecrets
    {
        private readonly FakeConnectionSecrets _inner = new();

        public Result<SecretPointer> Store(ConnectionName name, TokenSet tokens) => _inner.Store(name, tokens);

        public Result<TokenSet> Read(SecretPointer location)
        {
            reads.Add(HeldNow());
            return new TokenSet("access-stored", "refresh-stored", ExpiresAt: null, ClientSecret: "cs-stored");
        }

        public Result<Unit> Delete(SecretPointer location) => _inner.Delete(location);

        private bool HeldNow()
        {
            using var wait = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            try
            {
                if (gate.Enter(TimeProvider.System, wait.Token).AsTask().GetAwaiter().GetResult())
                {
                    gate.Exit();
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }
    }
}
