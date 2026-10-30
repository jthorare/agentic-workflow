using Aw.Kernel.Json;
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
    /// Unchecked syntax: whether rule paths are absolute or workspace-relative (this writes the path as given), the exact spelling of a Shell rule with arguments, and whether
    /// '--trust' is needed with '--workspace'.
    /// </para>
    /// </summary>
    /// <param name="policy">The Step's policy.</param>
    /// <returns>The JSON.</returns>
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

    private static string Rule(string tool) => tool switch
    {
        "Read" or "Grep" or "Glob" => "Read(**)",
        _ when tool.StartsWith("Bash(", StringComparison.Ordinal) => "Shell(" + tool["Bash(".Length..],
        _ when tool.StartsWith("Edit(", StringComparison.Ordinal) => "Write(" + tool["Edit(".Length..],
        _ => tool,
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

    /// <summary>Writes the config 0600 and the sidecar, unless the worktree already has a <c lang="text">.cursor/cli.json</c> (it is the repo's, and is never overwritten).</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="workDir">The Step's worktree.</param>
    /// <param name="policy">The Step's policy.</param>
    /// <returns>The config's path, or null when the worktree already has one.</returns>
    internal static string? Write(string directory, LaunchId launch, string workDir, StepPolicy policy)
    {
        var path = PathIn(workDir);
        if (File.Exists(path))
        {
            return null;
        }

        OwnerOnly.EnsureDirectory(directory);
        OwnerOnly.EnsureDirectory(Path.GetDirectoryName(path) ?? workDir);
        OwnerOnly.WriteAtomically(path, Render(policy));
        OwnerOnly.WriteAtomically(SidecarFor(directory, launch), System.Text.Encoding.UTF8.GetBytes(path));
        return path;
    }

    /// <summary>Deletes the launch's config (the one its sidecar names) and the sidecar; missing ones are fine.</summary>
    /// <param name="directory">The launcher's policy directory.</param>
    /// <param name="launch">The launch.</param>
    internal static void Delete(string directory, LaunchId launch)
    {
        var sidecar = SidecarFor(directory, launch);
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

        _ = OwnerOnly.TryDelete(sidecar);
    }
}
