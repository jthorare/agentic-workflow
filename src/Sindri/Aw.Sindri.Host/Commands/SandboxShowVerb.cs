using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox show [--plan &lt;file&gt;]' (sandbox spec §8): what the sandbox lets the session do, in four lines (Can write, Can read, Network, Limits) and then the whole plan. Like 'explain' it
/// runs before any service is built and reads only the file '--plan' or AW_SANDBOX_PLAN names, so a session answers with the state directory denied. Exit 0 answered, 2 usage or no readable plan.
/// </summary>
internal static class SandboxShowVerb
{
    internal const string Usage = "usage: sindri sandbox show [--plan <file>]";

    internal static async Task<int> Run(string[] args, Func<string, string?> environment, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        string? file = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--plan" && i + 1 < args.Length)
            {
                file = args[++i];
            }
            else
            {
                return await Fail(stderr, $"'{args[i]}' isn't an argument of this command, or it needs a value ({Usage})").ConfigureAwait(false);
            }
        }

        file ??= environment(SandboxEnvironment.Plan);
        if (string.IsNullOrWhiteSpace(file))
        {
            return await Fail(stderr, $"there is no sandbox plan file to read: this is not a sandboxed session (fix: pass --plan <file>, or run it inside a Step or `sindri open` session) ({Usage})").ConfigureAwait(false);
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return await Fail(stderr, $"the sandbox plan file {file} can't be read: {e.Message} (fix: check AW_SANDBOX_PLAN, or pass --plan <file>)").ConfigureAwait(false);
        }

        var parsed = AwJson.Deserialize<SandboxPlan>(bytes, AwJson.CreateOptions(SindriJsonContext.Default));
        if (parsed.IsError)
        {
            return await Fail(stderr, $"the sandbox plan file {file} isn't a plan this build reads: {parsed.Error.Message}").ConfigureAwait(false);
        }

        await stdout.WriteAsync(SandboxPlanRenderer.Lead(parsed.Value) + "\n" + SandboxPlanRenderer.Show(parsed.Value)).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> Fail(TextWriter stderr, string message)
    {
        await stderr.WriteLineAsync($"{SindriErrors.InvalidInput.Code} {message}").ConfigureAwait(false);
        return 2;
    }
}
