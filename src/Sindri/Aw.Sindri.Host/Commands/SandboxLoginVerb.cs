using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox login &lt;claude-code|codex&gt;' (sandbox spec SB-8, §3.4): signs the target in to its own agent home, the login Steps use when 'sandbox.login.source' is separate-login.
/// Human-only and not an API route: a session Sindri launched, or a CLI with no readable per-start token, is refused (SND-AUTH-002). Exit code: the sign-in's; 2 for usage or a refusal.
/// </summary>
/// <param name="output">Where the verb writes.</param>
/// <param name="services">The container: the login port.</param>
/// <param name="caller">Tells a human from a session.</param>
internal sealed class SandboxLoginVerb(Output output, IServiceProvider services, CallerHeaders caller)
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

        if (await caller.Resolve(ct).ConfigureAwait(false) is { } reason)
        {
            return await output.Error(SindriErrors.AuthRefused.With($"sandbox login is for the person at the keyboard, and this is {reason} (fix: run it from your own terminal)")).ConfigureAwait(false);
        }

        if (services.GetService<ISandboxLogin>() is not { } login)
        {
            return await output.Error(SindriErrors.LaunchSandboxUnavailable.With("sandbox login needs the Linux sandbox, and this build has none for this machine")).ConfigureAwait(false);
        }

        var ran = await login.Run(AgentId.From(word), ct).ConfigureAwait(false);
        return ran.IsError ? await output.Error(ran.Error).ConfigureAwait(false) : ran.Value;
    }
}
