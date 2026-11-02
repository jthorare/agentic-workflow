using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;
using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// Whether Jev can be asked on this box, the way the judge finds the key: the TypeSafe variable is set and non-empty, or, on a Mac, the keychain holds the entry
/// (`security find-generic-password -s &lt;service&gt; -a &lt;variable&gt;`, without -w so the secret is never read; exit 0 means found). A missing entry, a missing tool or a tool that hangs reads as no key.
/// The doctor row and the status share it.
/// </summary>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="runner">The judge's process runner, built on first use.</param>
/// <param name="isMacOS">Whether this is a Mac (the only place the judge reads the keychain).</param>
internal sealed class JevKey(Func<string, string?> environment, Func<IProcessRunner> runner, bool isMacOS) : IJevKeyProbe
{
    internal const string Variable = "TYPESAFE_API_KEY";

    // The keychain service the judge stores the key under (Aw.Judge.Infrastructure.Secrets.KeychainSecretSource).
    private const string Service = "agentic-workflow-typesafe";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public async ValueTask<bool> Present(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(environment(Variable)))
        {
            return true;
        }

        if (!isMacOS)
        {
            return false;
        }

        var result = await runner().Run(new ProcessRequest { FileName = "security", Arguments = ["find-generic-password", "-s", Service, "-a", Variable], Timeout = _timeout }, cancellationToken).ConfigureAwait(false);
        return result is { End: ProcessEnd.Exited, ExitCode: 0 };
    }
}
