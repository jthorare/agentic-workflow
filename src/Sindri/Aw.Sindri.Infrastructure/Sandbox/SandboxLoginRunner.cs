using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// 'sindri sandbox login' (sandbox spec SB-8): makes the target's agent home 0700, runs the toolkit's hook installer into it (--target-dir), then the target's own interactive sign-in
/// on this terminal with CLAUDE_CONFIG_DIR or CODEX_HOME pointing at the home. The toolkit checkout is TOOLKIT_DIR, else '&lt;AW_STATE_DIR or ~/.agentic-workflow&gt;/toolkit'.
/// </summary>
/// <param name="sindriHome">Sindri's own home.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="binDirectory">Where the agent CLI is looked for instead of PATH; null (the product) uses PATH.</param>
internal sealed class SandboxLoginRunner(string sindriHome, Func<string, string?> environment, string? binDirectory = null) : ISandboxLogin
{
    public async Task<Result<int>> Run(AgentId target, CancellationToken cancellationToken)
    {
        var (provider, variable, signIn) = target.Value switch
        {
            "claude-code" => ("claude", "CLAUDE_CONFIG_DIR", new[] { "claude" }),
            "codex" => ("codex", "CODEX_HOME", ["codex", "login"]),
            _ => (string.Empty, string.Empty, []),
        };
        if (provider.Length == 0)
        {
            return SindriErrors.InvalidInput.With($"'{target.Value}' has no separate login (claude-code and codex do)");
        }

        var home = AgentHomes.For(sindriHome, target);
        OwnerOnly.EnsureDirectory($"{sindriHome.TrimEnd('/')}/agent-homes");
        OwnerOnly.EnsureDirectory(home);

        var toolkit = ToolkitDirectory();
        var installer = Path.Combine(toolkit, "providers", provider, "install-hooks.sh");
        if (!File.Exists(installer))
        {
            return SindriErrors.LaunchLoginUnavailable.With($"the hook installer {installer} isn't there (fix: set TOOLKIT_DIR to the agentic-workflow checkout)");
        }

        var hooks = await RunOnTerminal("bash", [installer, "--target-dir", home], new Dictionary<string, string?>(StringComparer.Ordinal) { ["TOOLKIT_DIR"] = toolkit }, cancellationToken).ConfigureAwait(false);
        if (hooks.IsError || hooks.Value != 0)
        {
            return hooks.IsError ? hooks.Error : SindriErrors.LaunchLoginUnavailable.With($"{installer} exited {hooks.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}, so {home} has no hooks");
        }

        var signed = await RunOnTerminal(binDirectory is null ? signIn[0] : Path.Combine(binDirectory, signIn[0]), signIn[1..], new Dictionary<string, string?>(StringComparer.Ordinal) { [variable] = home }, cancellationToken).ConfigureAwait(false);
        if (signed.IsSuccess && signed.Value == 0 && string.Equals(provider, "codex", StringComparison.Ordinal))
        {
            await Console.Error.WriteLineAsync($"next: open codex with CODEX_HOME={home} and run /hooks to trust the aw:* entries (once)").ConfigureAwait(false);
        }

        return signed;
    }

    private string ToolkitDirectory()
    {
        if (environment("TOOLKIT_DIR") is { Length: > 0 } set)
        {
            return set;
        }

        var state = environment("AW_STATE_DIR") is { Length: > 0 } dir ? dir : Path.Combine(environment("HOME") ?? string.Empty, ".agentic-workflow");
        return Path.Combine(state, "toolkit");
    }

    // No redirection: the child has this terminal.
    private static async Task<Result<int>> RunOnTerminal(string program, IEnumerable<string> arguments, IReadOnlyDictionary<string, string?> extra, CancellationToken cancellationToken)
    {
        var info = ChildEnvironment.Start(program, arguments, extra);
        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                return SindriErrors.LaunchLoginUnavailable.With($"{program} did not start");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            return SindriErrors.LaunchLoginUnavailable.With($"{program} could not start: {e.Message} (fix: install it and put it on PATH)");
        }
    }
}
