using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The per-launch <c lang="text">.cursor/cli.json</c> behind the 'cursor-config' policy form (issue #1428): written 0600 into the Step's worktree and deleted
/// with the launch. Draft: the assumptions are at <see cref="Render"/>.
/// </summary>
internal static class CursorPolicyFile
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(LauncherJsonContext.Default, indented: true);

    /// <summary>
    /// The file's text for <paramref name="policy"/>: ReadOnly and Default allow writes to the one writable path, Plan allows none; the allowed tools map to Cursor's rule types
    /// (a bare Read, Grep or Glob to 'Read(**)', 'Bash(x)' to 'Shell(x)', Edit and Write to 'Write'); denied paths deny Read and Write, a read-only path denies Write.
    /// <para>
    /// DRAFT (issue #1428). Shape from Cursor's public CLI docs (permissions: allow and deny lists, rule types Shell, Read, Write, WebFetch and Mcp, deny wins; parameters: -p, --trust,
    /// --sandbox enabled|disabled, --workspace, --mode plan), not from a run. What each open question decides:
    /// Q1 (does a project .cursor/cli.json merge with the user's ~/.cursor/cli-config.json, or replace it?): if it merges, the user's own allow rules still apply and this file can only
    /// add allows and denies, so a Step is looser than its policy by whatever the user allowed; if the project file is ignored or a trust prompt hides it, the Step runs on the user's rules alone.
    /// Q2 (does headless '-p' without '--force' refuse a command no rule allows?): the whole form relies on it; if '-p' runs unlisted commands, the allow list confines nothing and the
    /// form must not ship.
    /// Q3 (does '--sandbox enabled' confine writes to the workspace?): the denied-path and read-only rules are the only write limit when it does not.
    /// Q4 (does '--mode plan' block ALL writes?): ReadOnly Steps get '--mode plan' plus Write(&lt;path&gt;/**), the same pattern as Claude's defaultMode plan; if Cursor's plan mode is strictly
    /// read-only, a ReadOnly Step can't write its output.
    /// Unchecked syntax: whether rule paths are absolute or workspace-relative (this writes the path as given), the exact spelling of a Shell rule with arguments, and whether
    /// '--trust' is needed with '--workspace'.
    /// </para>
    /// </summary>
    /// <param name="policy">The Step's policy.</param>
    /// <returns>The JSON.</returns>
    /// <exception cref="ArgumentException">An allowed tool has no Cursor rule type.</exception>
    internal static byte[] Render(StepPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var allow = policy.AllowedTools.Select(Rule).ToList();
        if (policy.Mode != StepPermissionMode.Plan && policy.WritablePath is { } path)
        {
            allow.Add($"Write({path}/**)");
        }

        var deny = new List<string>();
        foreach (var denied in policy.DeniedPaths ?? [])
        {
            deny.AddRange([$"Read({denied}/**)", $"Write({denied}/**)"]);
        }

        deny.AddRange((policy.ReadOnlyPaths ?? []).Select(static readOnly => $"Write({readOnly}/**)"));   // readable, never written
        var document = new CursorCliDocument(new CursorPermissions(allow) { Deny = deny.Count == 0 ? null : [.. deny] });
        return [.. AwJson.Serialize(document, _json), .. "\n"u8];
    }

    private static readonly string[] _cursorRules = ["Shell(", "Read(", "Write(", "WebFetch(", "Mcp("];

    private static string Rule(string tool) => tool switch
    {
        "Read" or "Grep" or "Glob" => "Read(**)",
        "Bash" => "Shell(*)",
        "Edit" or "Write" => "Write(**)",
        _ when tool.StartsWith("Bash(", StringComparison.Ordinal) => "Shell(" + tool["Bash(".Length..],
        _ when tool.StartsWith("Edit(", StringComparison.Ordinal) => "Write(" + tool["Edit(".Length..],
        _ when _cursorRules.Any(prefix => tool.StartsWith(prefix, StringComparison.Ordinal)) => tool,
        _ => throw new ArgumentException($"the allowed tool '{tool}' has no Cursor rule type", nameof(tool)),
    };

    /// <summary>Where the config lives in <paramref name="workDir"/>.</summary>
    /// <param name="workDir">The Step's worktree.</param>
    /// <returns>The file's path.</returns>
    internal static string PathIn(string workDir) => Path.Combine(workDir, ".cursor", "cli.json");

    /// <summary>The sidecar under the policy directory that remembers where the launch's file is, so the launch's cleanup can delete it without the worktree.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>The sidecar's path.</returns>
    internal static string SidecarFor(string directory, LaunchId launch) => Path.Combine(directory, launch.Value + ".cursor-path");

    /// <summary>
    /// Writes the config 0600 and the sidecar. A <c lang="text">.cursor/cli.json</c> a sidecar names is a launch's own: replaced when that launch is not live (its host died), refused with its own message
    /// when it is. One no sidecar names is the repo's, and is never overwritten.
    /// </summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="workDir">The Step's worktree.</param>
    /// <param name="policy">The Step's policy.</param>
    /// <param name="live">Whether a launch has a live session (the sweep's own test).</param>
    /// <returns>The config's path, or the refusal.</returns>
    internal static Result<string> Write(string directory, LaunchId launch, string workDir, StepPolicy policy, Func<LaunchId, bool> live)
    {
        ArgumentNullException.ThrowIfNull(live);
        byte[] rendered;
        try
        {
            rendered = Render(policy);
        }
        catch (ArgumentException e)
        {
            return SindriErrors.RequestInvalid.With($"the Step's policy can't be written for Cursor: {e.Message}");
        }

        var path = PathIn(workDir);
        if (File.Exists(path))
        {
            if (Holder(directory, path) is not { } holder)
            {
                return SindriErrors.RequestInvalid.With($"the Step's worktree already has a {path}, which a launch never overwrites (fix: remove it or move its rules to the user config)");
            }

            if (holder != launch && live(holder))
            {
                return SindriErrors.RequestInvalid.With($"the Step's worktree is in use by another launch ({holder.Value}), whose {path} is still live (fix: wait for it to end or stop it)");
            }

            Delete(directory, holder);   // a dead launch's file and sidecar
        }

        OwnerOnly.EnsureDirectory(directory);
        OwnerOnly.EnsureDirectory(Path.GetDirectoryName(path) ?? workDir);
        OwnerOnly.WriteAtomically(path, rendered);
        OwnerOnly.WriteAtomically(SidecarFor(directory, launch), System.Text.Encoding.UTF8.GetBytes(path));
        return path;
    }

    /// <summary>The launch whose sidecar names <paramref name="path"/>, or null when none does.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="path">A config file's path.</param>
    /// <returns>The launch.</returns>
    internal static LaunchId? Holder(string directory, string path)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        foreach (var sidecar in Directory.EnumerateFiles(directory, "LCH-*.cursor-path"))
        {
            var name = Path.GetFileName(sidecar);
            try
            {
                if (LaunchId.TryParse(name[..name.IndexOf('.', StringComparison.Ordinal)], provider: null, out var holder) && string.Equals(File.ReadAllText(sidecar).Trim(), path, StringComparison.Ordinal))
                {
                    return holder;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // an unreadable sidecar names nothing we can prove
            }
        }

        return null;
    }

    /// <summary>Deletes the launch's config (the one its sidecar names) and the sidecar; missing ones are fine.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    internal static void Delete(string directory, LaunchId launch) => DeleteSidecar(SidecarFor(directory, launch));

    /// <summary>Deletes the config <paramref name="sidecar"/> names and then the sidecar; the sweep's delete for a stale one.</summary>
    /// <param name="sidecar">A launch's <c lang="text">.cursor-path</c> file.</param>
    /// <returns>Whether the sidecar is gone now.</returns>
    internal static bool DeleteSidecar(string sidecar)
    {
        try
        {
            if (File.Exists(sidecar))
            {
                _ = OwnerOnly.TryDelete(File.ReadAllText(sidecar).Trim());
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the sidecar goes below; a config that can't be read back is left for the worktree's removal
        }

        return OwnerOnly.TryDelete(sidecar);
    }
}
