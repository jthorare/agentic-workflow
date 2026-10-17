using System.Text.Json;
using Aw.Forge.Core;
using Aw.Forge.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.Credentials;

/// <summary>
/// A token, bound to one host, from a command run per call (command:&lt;argv&gt;). The command prints either a bare token or JSON
/// {"token": "...", "expiresAt": "ISO-8601"}. A token with an expiry is cached until five minutes before it; a bare token is
/// cached for five minutes. A refresh (after a 401) runs the command again.
/// </summary>
internal sealed class CommandCredentialSource(string boundHost, IReadOnlyList<string> argv, TimeProvider clock) : ICredentialSource, IDisposable
{
    private static readonly TimeSpan _margin = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _bareLifetime = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _validUntil;

    public async ValueTask<Result<string>> Token(string host, bool refresh, CancellationToken cancellationToken)
    {
        if (!BoundHost.Matches(boundHost, host))
        {
            return BoundHost.Refusal(host);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!refresh && _token is not null && clock.GetUtcNow() < _validUntil)
            {
                return _token;
            }

            var ran = await ProcessRunner.Run(new ProcessSpec(argv[0], [.. argv.Skip(1)], new Dictionary<string, string>(StringComparer.Ordinal), WorkingDirectory: null, Timeout: TimeSpan.FromSeconds(30)), cancellationToken).ConfigureAwait(false);
            if (ran is not { ExitCode: 0 })
            {
                return ForgeErrors.Unauthorized.With("the credential command failed");
            }

            return Accept(ran.Output.Trim());
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private Result<string> Accept(string output)
    {
        var now = clock.GetUtcNow();
        var token = output;
        var until = now + _bareLifetime;
        if (output.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(output);
                var root = document.RootElement;
                token = root.TryGetProperty("token", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? string.Empty : string.Empty;
                if (root.TryGetProperty("expiresAt", out var e) && e.ValueKind == JsonValueKind.String && e.TryGetDateTimeOffset(out var expiry))
                {
                    until = expiry - _margin;
                }
            }
            catch (JsonException)
            {
                return ForgeErrors.Unauthorized.With("the credential command printed invalid JSON");
            }
        }

        if (token.Length == 0)
        {
            return ForgeErrors.Unauthorized.With("the credential command printed no token");
        }

        _token = token;
        _validUntil = until;
        return token;
    }

    public void Dispose() => _gate.Dispose();
}
