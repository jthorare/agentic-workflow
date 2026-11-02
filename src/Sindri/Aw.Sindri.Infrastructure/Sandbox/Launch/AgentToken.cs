namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>A minted installation token and when GitHub stops honoring it. The value never prints.</summary>
/// <param name="Value">The token.</param>
/// <param name="ExpiresAt">When it expires.</param>
internal sealed record AgentToken(string Value, DateTimeOffset ExpiresAt)
{
    /// <inheritdoc/>
    public override string ToString() => $"AgentToken(expires {ExpiresAt:O})";
}
