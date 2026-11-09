using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox login &lt;claude-code|codex&gt;' (sandbox spec SB-8, §3.4): signs the target in to its own agent home, the login Steps use when 'sandbox.login.source' is separate-login.
/// Not an API route; the owner and an agent session run it alike. Exit code: the sign-in's; 2 for usage or a refusal.
/// </summary>
/// <param name="output">Where the verb writes.</param>
/// <param name="services">The container: the login port.</param>
internal sealed class SandboxLoginVerb(Output output, IServiceProvider services)
{
    internal const string Usage = "usage: sindri sandbox login <claude-code|codex>";

    private static readonly string[] _targets = ["claude-code", "codex"];

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args is not [var word] || !_targets.Contains(word, StringComparer.Ordinal))
        {
            return await output.Error(SindriErrors.InvalidInput.With("login needs one target, claude-code or codex (" + Usage + ")")).ConfigureAwait(false);
        }

        if (services.GetService<ISandboxLogin>() is not { } login)
        {
            return await output.Error(SindriErrors.LaunchSandboxUnavailable.With("sandbox login needs the sandbox, and this build has none for this machine")).ConfigureAwait(false);
        }

        var ran = await login.Run(AgentId.From(word), ct).ConfigureAwait(false);
        return ran.IsError ? await output.Error(ran.Error).ConfigureAwait(false) : ran.Value;
    }
}
