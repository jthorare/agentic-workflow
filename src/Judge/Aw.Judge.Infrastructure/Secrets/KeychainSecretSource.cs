using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;
using Aw.Judge.Core.Secrets;

namespace Aw.Judge.Infrastructure.Secrets;

/// <summary>
/// ISecretSource over the environment, then the macOS keychain: a secret's variable (SecretNames) wins when it is set and non-empty;
/// otherwise `security find-generic-password -s &lt;service&gt; -a &lt;variable&gt; -w` prints it. A keychain without
/// the entry, a keychain tool that isn't there (not macOS) or one that hangs past its timeout all read as unset.
/// </summary>
/// <param name="environment">Reads the secret's variable.</param>
/// <param name="runner">Runs the keychain tool.</param>
internal sealed class KeychainSecretSource(Func<string, string?> environment, IProcessRunner runner) : ISecretSource
{
    // The keychain tool answers in milliseconds; a locked keychain's prompt must not hold the judge.
    private static readonly TimeSpan _keychainTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public async ValueTask<string?> Read(SecretName name, CancellationToken cancellationToken)
    {
        var (variable, service) = Entry(name);
        if (environment(variable) is { Length: > 0 } value)
        {
            return value;
        }

        var result = await runner.Run(
            new ProcessRequest
            {
                FileName = "security",
                Arguments = ["find-generic-password", "-s", service, "-a", variable, "-w"],
                Timeout = _keychainTimeout,
            },
            cancellationToken).ConfigureAwait(false);
        var secret = result.ExitCode == 0 ? result.StandardOutput.Trim() : "";
        return secret.Length > 0 ? secret : null;
    }

    // The variable (SecretNames), which is also the keychain account, and the keychain service of each secret.
    private static (string Variable, string Service) Entry(SecretName name) => (SecretNames.Variable(name), name switch
    {
        SecretName.TypeSafeApiKey => "agentic-workflow-typesafe",
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown secret"),
    });
}
