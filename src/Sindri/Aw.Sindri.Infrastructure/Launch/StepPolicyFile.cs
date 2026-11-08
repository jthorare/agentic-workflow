using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The 0600 settings file behind the 'claude-settings-file' policy form (launcher L-17(a), owner 2026-10-17): the launcher renders the target-neutral
/// <see cref="StepPolicy"/> and deletes the file with the launch. ReadOnly is plan mode plus the allow-list and the one writable path; Plan is plan mode
/// with no write rules; Default sets no mode and adds the allow-list and the writable path.
/// </summary>
internal static class StepPolicyFile
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(LauncherJsonContext.Default, indented: true);

    /// <summary>The file's text for <paramref name="policy"/>.</summary>
    /// <param name="policy">The Step's policy.</param>
    /// <param name="sandbox">The sandbox plan's contribution (its rules, 'dontAsk', bypass disabled, the CLI's own sandbox switch), or null for an unconfined launch.</param>
    /// <returns>The settings JSON.</returns>
    internal static byte[] Render(StepPolicy policy, SandboxPolicyOverlay? sandbox = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var allow = new List<string>(policy.AllowedTools);
        string[]? directories = null;
        if (policy.Mode != StepPermissionMode.Plan && policy.WritablePath is { } path)
        {
            allow.AddRange([$"Edit(/{path}/**)", $"Write(/{path}/**)"]);
            directories = [path];
        }

        var deny = new List<string>();
        foreach (var denied in policy.DeniedPaths ?? [])
        {
            deny.AddRange([$"Read(/{denied}/**)", $"Edit(/{denied}/**)"]);
        }

        foreach (var readOnly in policy.ReadOnlyPaths ?? [])
        {
            deny.Add($"Edit(/{readOnly}/**)");   // readable through the directory, never edited
            directories = [.. directories ?? [], readOnly];
        }

        var permissions = new ClaudePermissions(allow)
        {
            Deny = deny.Count == 0 ? null : [.. deny],
            DefaultMode = policy.Mode == StepPermissionMode.Default ? null : "plan",
            AdditionalDirectories = directories,
        };
        var document = Document(permissions, policy, sandbox);
        return [.. AwJson.Serialize(document, _json), .. "\n"u8];
    }

    private static ClaudeSettingsDocument Document(ClaudePermissions permissions, StepPolicy policy, SandboxPolicyOverlay? sandbox) => sandbox is null
        ? new ClaudeSettingsDocument(permissions, TurnHooks(policy.TurnMarkers))
        : new ClaudeSettingsDocument(SandboxPolicyFiles.Merge(permissions, policy.Mode, sandbox), TurnHooks(policy.TurnMarkers))
        {
            Sandbox = new ClaudeSandbox(sandbox.InnerSandboxOn),
        };

    // The Step's session tells Sindri when its turn ends and starts through marker files (TurnHookCommands); Claude Code runs them as settings hooks.
    // StopFailure, which runs instead of Stop when an API error ends the turn, touches .turn-failed.
    private static ClaudeHooks TurnHooks(bool markers)
    {
        static ClaudeHookGroup[] Run(string command) => [new ClaudeHookGroup([new ClaudeHookCommand("command", command)])];
        return new ClaudeHooks(
            Stop: Run(TurnHookCommands.Stop),
            StopFailure: Run(TurnHookCommands.StopFailure),
            UserPromptSubmit: Run(TurnHookCommands.Prompt(markers)),
            PreToolUse: Run(TurnHookCommands.PreTool),
            PostToolUse: Run(TurnHookCommands.PostTool),
            PostToolUseFailure: Run(TurnHookCommands.PostTool));
    }

    /// <summary>Where the launch's settings file lives under <paramref name="directory"/>.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The file's path.</returns>
    internal static string PathFor(string directory, LaunchId launch) => Path.Combine(directory, launch.Value + ".settings.json");

    /// <summary>Writes the file 0600 (the directory 0700) through a temp file and a rename.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="policy">The Step's policy.</param>
    /// <param name="sandbox">The sandbox plan's contribution, or null for an unconfined launch.</param>
    /// <returns>The file's path.</returns>
    internal static string Write(string directory, LaunchId launch, StepPolicy policy, SandboxPolicyOverlay? sandbox = null)
    {
        OwnerOnly.EnsureDirectory(directory);
        var path = PathFor(directory, launch);
        OwnerOnly.WriteAtomically(path, Render(policy, sandbox));
        return path;
    }

    /// <summary>Deletes the launch's file; a missing one is fine.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    internal static void Delete(string directory, LaunchId launch) => _ = OwnerOnly.TryDelete(PathFor(directory, launch));
}
