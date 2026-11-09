using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Testing.Connections;

/// <summary>IOAuthClient with scripted answers: what Refresh gives back, and the calls it saw. The token endpoint's own wire is FakeOAuthServer's.</summary>
public sealed class FakeOAuthClient : IOAuthClient
{
    /// <summary>Gets the refresh calls: the preset key, the client id and the refresh token presented.</summary>
    public IList<(string Preset, string Client, string RefreshToken)> Refreshes { get; } = [];

    /// <summary>Gets the client secret each refresh call presented, in call order (null for a public client).</summary>
    public IList<string?> RefreshClientSecrets { get; } = [];

    /// <summary>Gets or sets the tokens the next refreshes return (a provider that keeps the old refresh token returns null for it).</summary>
    public TokenSet? Next { get; set; }

    /// <summary>Gets or sets the provider error code every refresh is refused with ('invalid_grant'); null lets refreshes succeed.</summary>
    public string? RefuseWith { get; set; }

    /// <summary>Gets or sets whether every refresh fails as the real client does when the token endpoint cannot be reached: a refresh failure with no provider code.</summary>
    public bool Unreachable { get; set; }

    /// <summary>Gets or sets what runs after the refresh call is made and before it answers (a sign-in or removal that lands while the provider is slow).</summary>
    public Func<ValueTask>? DuringRefresh { get; set; }

    /// <summary>Gets or sets text the failure messages carry, so a test can prove the provider's wording never reaches a row, the ledger or a log (a real provider's text can repeat a token).</summary>
    public string? ErrorCanary { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<TokenSet>> ExchangeCode(CodeExchange exchange, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<TokenSet>>(Next ?? throw new InvalidOperationException("set Next first"));

    /// <inheritdoc />
    public async ValueTask<Result<TokenSet>> Refresh(ConnectionPreset preset, string client, string refreshToken, string? clientSecret, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        Refreshes.Add((preset.Key, client, refreshToken));
        RefreshClientSecrets.Add(clientSecret);
        if (DuringRefresh is { } during)
        {
            await during().ConfigureAwait(false);
        }

        if (Unreachable)
        {
            return SindriErrors.ConnectionRefreshFailed.With($"the token endpoint could not be reached{Canary()}");
        }

        if (RefuseWith is { } code)
        {
            // the provider's own error code rides in the 'providerCode' extension, so the caller can name it without parsing text
            return SindriErrors.ConnectionRefreshFailed.With($"the token endpoint answered 400 ({code}){Canary()}").WithExtension("providerCode", JsonSerializer.SerializeToElement(code));
        }

        return Next ?? throw new InvalidOperationException("set Next first");
    }

    private string Canary() => ErrorCanary is { } text ? " " + text : string.Empty;

    /// <summary>Gets or sets what the next MCP registration returns; null refuses it.</summary>
    public McpSignInTarget? McpTarget { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<McpSignInTarget>> RegisterMcpClient(Uri server, string redirectUri, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<McpSignInTarget>>(McpTarget is { } target ? target : SindriErrors.ConnectionRefreshFailed.With("the fake MCP server offers no sign-in"));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Revoke(ConnectionPreset preset, string client, string token, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<Unit>>(Unit.Value);
}
