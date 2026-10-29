using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Secrets;

/// <summary>
/// ISecretWriter (spec §5.3). The secret reaches the store on the tool's standard input, never as an argument: on macOS `security -i` is given its one command on stdin; on Linux `secret-tool store` reads the secret from stdin.
/// When the store isn't there, is locked or refuses, the secret goes to an owner-only file under the Sindri home (the directory 0700, the file 0600, replaced atomically) and the pointer is a `file:` one. The secret is in no error message.
/// </summary>
/// <param name="runner">Runs `security` and `secret-tool`.</param>
/// <param name="home">The Sindri home the file fallback lives under.</param>
/// <param name="platform">Which store to use; null takes this machine's.</param>
internal sealed class KeychainSecretWriter(IToolRunner runner, ISindriHome home, KeychainPlatform? platform = null) : ISecretWriter
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);   // a locked keychain's unlock prompt may need the owner a moment

    private readonly KeychainPlatform _platform = platform ?? (OperatingSystem.IsMacOS() ? KeychainPlatform.MacOS : OperatingSystem.IsLinux() ? KeychainPlatform.Linux : KeychainPlatform.Unsupported);

    /// <inheritdoc />
    public Result<SecretPointer> Store(string service, string account, string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        var pointer = SecretPointer.Parse($"keychain:{service}/{account}");
        if (pointer.IsError)
        {
            return SindriErrors.SecretNotStored.With("the key's store name is malformed");   // unreachable for the fixed names Sindri passes
        }

        if (IsPlain(secret) && StoredInKeychain(service, account, secret) && ReadsBack(pointer.Value, secret))
        {
            return pointer;
        }

        return ToFile(service, account, secret);
    }

    /// <inheritdoc />
    public Result<SecretPointer> StoreInFile(string service, string account, string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return ToFile(service, account, secret);
    }

    // The secret goes into a command line parsed by `security -i`, so only a plain token (what a source token is) is sent that way; anything else takes the file.
    private static bool IsPlain(string secret) => secret.Length > 0 && secret.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '~' or '+' or '/' or '=' or '-');

    private bool StoredInKeychain(string service, string account, string secret)
    {
        var run = _platform switch
        {
            KeychainPlatform.MacOS => runner.Run("security", ["-i"], _timeout, new ToolRunOptions(StandardInput: $"add-generic-password -U -s {service} -a {account} -w {secret}\n")),
            KeychainPlatform.Linux => runner.Run("secret-tool", ["store", "--label", $"{service}/{account}", "service", service, "account", account], _timeout, new ToolRunOptions(StandardInput: secret)),
            KeychainPlatform.Unsupported => ToolRun.Missing,
            _ => throw new ArgumentOutOfRangeException(nameof(service), _platform, "unknown platform"),
        };
        return run is { Outcome: ToolOutcome.Exited, ExitCode: 0 };
    }

    // `security -i` can exit 0 after a command it refused, so a clean exit isn't proof: the pointer must resolve to the secret just stored, or the file takes it.
    private bool ReadsBack(SecretPointer pointer, string secret)
    {
        var read = new PointerSecrets(static _ => null, runner, _platform).Resolve(pointer);
        return read is { IsSuccess: true } && string.Equals(read.Value, secret, StringComparison.Ordinal);
    }

    private Result<SecretPointer> ToFile(string service, string account, string secret)
    {
        var directory = Path.Combine(home.Path, "secrets");
        var path = Path.Combine(directory, $"{service}-{account}");
        try
        {
            OwnerOnly.EnsureDirectory(directory);
            var temporary = path + ".tmp";
            using (var stream = OpenOwnerOnly(temporary))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(secret);
            }

            File.Move(temporary, path, overwrite: true);
            OwnerOnly.RestrictFiles(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.SecretNotStored.With(
                $"neither the OS secret store nor {directory} could take the key ({e.GetType().Name}); make that directory writable by you, or install libsecret-tools (Linux), then run `sindri index connect <source>` again");
        }

        return SecretPointer.Parse($"file:{path}");
    }

    // Created 0600 from the start, so the secret is never in a file others can read, not even for a moment.
    private static FileStream OpenOwnerOnly(string path)
        => OperatingSystem.IsWindows()
            ? new FileStream(path, FileMode.Create, FileAccess.Write)
            : new FileStream(path, new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
}
