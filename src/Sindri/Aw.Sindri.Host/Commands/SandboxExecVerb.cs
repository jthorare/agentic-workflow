using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox exec &lt;launch|run-area&gt; [--json] [-- &lt;command...&gt;]' (sandbox spec §3.6): the human's way to see what a sandboxed session saw. It runs the command (a shell without one) in a fresh sandbox built from the launch's
/// stored plan, with the worktree, the run's ref directories and the run directory read-only, a fresh scratch, no launch secret and no GitHub token, in the Steps' slice and counted against the session cap.
/// Human-only and not an API route: a session Sindri launched, or a CLI with no readable per-start token, is refused (SND-AUTH-002). Exit code: the command's; 2 for usage, a refusal or a sandbox that can't be made.
/// </summary>
/// <param name="output">Where the verb writes.</param>
/// <param name="services">The container: the exec service.</param>
/// <param name="caller">Tells a human from a session.</param>
internal sealed class SandboxExecVerb(Output output, IServiceProvider services, CallerHeaders caller)
{
    internal const string Usage = "usage: sindri sandbox exec <launch|run-area> [--json] [-- <command...>]";

    private const string PlanFile = "sandbox-plan.json";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var (target, json, command, problem) = Parse(args);
        if (problem is not null)
        {
            return await output.Error(SindriErrors.InvalidInput.With(problem + " (" + Usage + ")")).ConfigureAwait(false);
        }

        if (await caller.Resolve(ct).ConfigureAwait(false) is { } reason)
        {
            return await output.Error(SindriErrors.AuthRefused.With($"sandbox exec is for the person at the keyboard, and this is {reason} (fix: run it from your own terminal)")).ConfigureAwait(false);
        }

        if (services.GetService<ISandboxExec>() is not { } exec)
        {
            return await output.Error(SindriErrors.LaunchSandboxUnavailable.With("sandbox exec needs the Linux sandbox, and this build has none for this machine")).ConfigureAwait(false);
        }

        var read = await ReadPlan(FileFor(target, exec), ct).ConfigureAwait(false);
        if (read.IsError)
        {
            return await output.Error(read.Error).ConfigureAwait(false);
        }

        var ran = await exec.Run(read.Value, command, ct).ConfigureAwait(false);
        if (ran.IsError)
        {
            return await output.Error(ran.Error).ConfigureAwait(false);
        }

        return json ? await output.Text(AsJson(read.Value.Launch, ran.Value), ran.Value).ConfigureAwait(false) : ran.Value;
    }

    private static (string Target, bool Json, IReadOnlyList<string> Command, string? Problem) Parse(string[] args)
    {
        var split = Array.IndexOf(args, "--");
        var head = split < 0 ? args : args[..split];
        IReadOnlyList<string> command = split < 0 || split == args.Length - 1 ? ["/bin/sh"] : args[(split + 1)..];
        var json = head.Contains("--json", StringComparer.Ordinal);
        var words = head.Where(static a => !string.Equals(a, "--json", StringComparison.Ordinal)).ToList();
        if (words.Find(static a => a.StartsWith("--", StringComparison.Ordinal)) is { } unknown)
        {
            return (string.Empty, false, command, $"'{unknown}' isn't an argument of this command");
        }

        return words.Count == 1 ? (words[0], json, command, null) : (string.Empty, false, command, "exec needs one launch id or run-area path");
    }

    private static async Task<Kernel.Result<SandboxPlan>> ReadPlan(string file, CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.InvalidInput.With($"the sandbox plan {file} can't be read: {e.Message} (fix: pass a launch id Sindri started, or its run area)");
        }

        var plan = AwJson.Deserialize<SandboxPlan>(bytes, _json);
        return plan.IsSuccess ? plan : SindriErrors.InvalidInput.With($"the sandbox plan {file} isn't a plan this build reads: {plan.Error.Message}");
    }

    // A launch id names the launcher's area for it; anything else is a run area (or the plan file itself).
    private static string FileFor(string target, ISandboxExec exec)
    {
        if (LaunchId.TryFrom(target, out var launch))
        {
            return exec.PlanFileOf(launch);
        }

        return Directory.Exists(target) ? Path.Combine(target, PlanFile) : target;
    }

    private static string AsJson(LaunchId launch, int exitCode)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("launch", launch.Value);
            writer.WriteNumber("exitCode", exitCode);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }
}
