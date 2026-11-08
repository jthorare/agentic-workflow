using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Testing.Connections;

/// <summary>ISignInSession whose completion a test finishes by hand: it opens no listener and no browser.</summary>
public sealed class FakeSignInSession : ISignInSession
{
    private readonly TaskCompletionSource<Result<TokenSet>> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the consent URL every start returns.</summary>
    public Uri ConsentUrl { get; } = new("https://provider.example.test/authorize?state=fake");

    /// <summary>Gets the clients the sign-ins were started for.</summary>
    public IList<string> Clients { get; } = [];

    /// <summary>Gets the scopes of the last start.</summary>
    public IReadOnlyList<string> LastScopes { get; private set; } = [];

    /// <summary>Ends the sign-in with these tokens.</summary>
    /// <param name="tokens">The tokens.</param>
    public void Succeed(TokenSet tokens) => _completion.SetResult(tokens);

    /// <summary>Ends the sign-in with an error.</summary>
    /// <param name="error">The error.</param>
    public void Fail(Error error) => _completion.SetResult(error);

    /// <inheritdoc />
    public ValueTask<Result<SignInStarted>> Start(ConnectionPreset preset, string client, IReadOnlyList<string> scopes, CancellationToken cancellationToken)
    {
        Clients.Add(client);
        LastScopes = scopes;
        return ValueTask.FromResult<Result<SignInStarted>>(new SignInStarted(ConsentUrl, _completion.Task));
    }
}
