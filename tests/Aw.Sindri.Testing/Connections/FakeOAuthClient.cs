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

    /// <summary>Gets or sets the tokens the next refreshes return (a provider that keeps the old refresh token returns null for it).</summary>
    public TokenSet? Next { get; set; }

    /// <summary>Gets or sets the provider error code every refresh is refused with ('invalid_grant'); null lets refreshes succeed.</summary>
    public string? RefuseWith { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<TokenSet>> ExchangeCode(CodeExchange exchange, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<TokenSet>>(Next ?? throw new InvalidOperationException("set Next first"));

    /// <inheritdoc />
    public ValueTask<Result<TokenSet>> Refresh(ConnectionPreset preset, string client, string refreshToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preset);
        Refreshes.Add((preset.Key, client, refreshToken));
        if (RefuseWith is { } code)
        {
            // the provider's own error code rides in the 'providerCode' extension, so the caller can name it without parsing text
            var refused = SindriErrors.ConnectionRefreshFailed.With($"the token endpoint answered 400 ({code})").WithExtension("providerCode", JsonSerializer.SerializeToElement(code));
            return ValueTask.FromResult<Result<TokenSet>>(refused);
        }

        return ValueTask.FromResult<Result<TokenSet>>(Next ?? throw new InvalidOperationException("set Next first"));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Revoke(ConnectionPreset preset, string client, string token, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<Unit>>(Unit.Value);
}
