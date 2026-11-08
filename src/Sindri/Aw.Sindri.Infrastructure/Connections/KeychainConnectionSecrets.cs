using System.Buffers.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// IConnectionSecrets over the existing secret writer and pointer resolver: ONE secret per connection, the <see cref="TokenSet"/> as JSON then
/// base64url (the keychain writer's plain-token rule rejects JSON characters; base64url passes it). Service 'sindri-connection', account = the
/// name with ':' as '.'. Delete runs 'security delete-generic-password' (macOS) or 'secret-tool clear' (Linux) through the tool runner; a file
/// pointer's file is deleted. No token is in an argument list or in any error text.
/// </summary>
/// <param name="writer">Stores the secret (keychain, else an owner-only file).</param>
/// <param name="pointers">Reads it back.</param>
/// <param name="runner">Runs the delete tool.</param>
/// <param name="platform">Which store to delete from; null takes this machine's.</param>
internal sealed class KeychainConnectionSecrets(ISecretWriter writer, ISecretPointers pointers, IToolRunner runner, KeychainPlatform? platform = null) : IConnectionSecrets
{
    private readonly ISecretWriter _writer = writer;
    private readonly ISecretPointers _pointers = pointers;
    private readonly IToolRunner _runner = runner;
    private const string Service = "sindri-connection";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    private readonly KeychainPlatform _platform = platform ?? (OperatingSystem.IsMacOS() ? KeychainPlatform.MacOS : OperatingSystem.IsLinux() ? KeychainPlatform.Linux : KeychainPlatform.Unsupported);

    public Result<SecretPointer> Store(ConnectionName name, TokenSet tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var stored = _writer.Store(Service, name.Service + "." + name.Label, Encode(tokens));
        return stored.IsSuccess
            ? stored.Value
            : SindriErrors.ConnectionSecretUnavailable.With("the secret store would not take the connection's tokens (" + stored.Error.Code + ")");
    }

    public Result<TokenSet> Read(SecretPointer pointer)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        var read = _pointers.Resolve(pointer);
        if (read.IsError)
        {
            return SindriErrors.ConnectionSecretUnavailable.With("the connection's tokens could not be read (" + read.Error.Code + ")");
        }

        var tokens = Decode(read.Value);
        return tokens is null
            ? SindriErrors.ConnectionSecretUnavailable.With("the stored connection secret is not a token set")
            : tokens;
    }

    public Result<Unit> Delete(SecretPointer pointer)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        switch (pointer.Scheme)
        {
            case SecretScheme.Keychain:
                return DeleteFromKeychain(pointer.Target.Split('/'));
            case SecretScheme.File:
                try
                {
                    File.Delete(pointer.Target);
                    return Unit.Value;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    return SindriErrors.ConnectionSecretUnavailable.With("the connection's secret file could not be deleted");
                }

            case SecretScheme.Env:
            case SecretScheme.OnePassword:
            default:
                return Unit.Value;   // an env or op pointer is not Sindri's to delete
        }
    }

    private static string Encode(TokenSet tokens)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("access", tokens.AccessToken);
            if (tokens.RefreshToken is not null)
            {
                writer.WriteString("refresh", tokens.RefreshToken);
            }

            if (tokens.ExpiresAt is { } expires)
            {
                writer.WriteString("expiresAt", expires);
            }

            writer.WriteEndObject();
        }

        return Base64Url.EncodeToString(buffer.ToArray());
    }

    private static TokenSet? Decode(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(Base64Url.DecodeFromChars(text.Trim()));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("access", out var access) || access.ValueKind != JsonValueKind.String || access.GetString() is not { } accessToken)
            {
                return null;
            }

            var refresh = root.TryGetProperty("refresh", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
            DateTimeOffset? expires = root.TryGetProperty("expiresAt", out var e) && e.ValueKind == JsonValueKind.String && e.TryGetDateTimeOffset(out var at) ? at : null;
            return new TokenSet(accessToken, refresh, expires);
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }

    private Result<Unit> DeleteFromKeychain(string[] segments)
    {
        var service = segments[0];
        var account = segments[1];
        var run = _platform switch
        {
            KeychainPlatform.MacOS => _runner.Run("security", ["delete-generic-password", "-s", service, "-a", account], _timeout),
            KeychainPlatform.Linux => _runner.Run("secret-tool", ["clear", "service", service, "account", account], _timeout),
            KeychainPlatform.Unsupported => ToolRun.Missing,
            _ => ToolRun.Missing,
        };

        // why: nothing stored counts as deleted: security exits 44, secret-tool 1 (or 0) for an entry that is not there
        return run is { Outcome: ToolOutcome.Exited, ExitCode: 0 or 1 or 44 }
            ? Unit.Value
            : SindriErrors.ConnectionSecretUnavailable.With("the OS keychain would not delete the connection's secret (none installed, or locked)");
    }
}
