using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The turn hooks of a Codex skill Step (skill flows S-0, Codex section): Codex has no settings file to hand over like Claude Code's, so the Stop, UserPromptSubmit, PreToolUse and PostToolUse hooks that touch the
/// launch's marker files (<see cref="TurnHookCommands"/>) go in the worktree's project file '.codex/hooks.json', which Codex runs when started with '--dangerously-bypass-hook-trust' (S-0 ran them that way). The user's
/// own '~/.codex' is untouched; the file is git-excluded so the Step's 'git add' never commits it. Codex has no StopFailure event, and a tool failure runs PostToolUse.
/// </summary>
internal static class CodexTurnHooks
{
    /// <summary>The file, relative to the worktree.</summary>
    internal const string RelativePath = ".codex/hooks.json";

    /// <summary>The flag that lets Codex run the project's hooks without persisted trust; the hooks are ours, so the file is vetted.</summary>
    internal const string TrustFlag = "--dangerously-bypass-hook-trust";

    /// <summary>The file's text.</summary>
    /// <returns>The JSON, one trailing newline.</returns>
    internal static byte[] Render()
    {
        static JsonObject Hook(string command, string? matcher = null)
        {
            var group = new JsonObject();
            if (matcher is not null)
            {
                group["matcher"] = matcher;
            }

            group["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command });
            return group;
        }

        var document = new JsonObject
        {
            ["hooks"] = new JsonObject
            {
                ["Stop"] = new JsonArray(Hook(TurnHookCommands.Stop)),
                ["UserPromptSubmit"] = new JsonArray(Hook(TurnHookCommands.Prompt(markers: true))),
                ["PreToolUse"] = new JsonArray(Hook(TurnHookCommands.PreTool, ".*")),
                ["PostToolUse"] = new JsonArray(Hook(TurnHookCommands.PostTool, ".*")),
            },
        };
        return [.. Encoding.UTF8.GetBytes(document.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })), .. "\n"u8];
    }

    /// <summary>Writes the file into the worktree and git-excludes it. A file already there that is not exactly ours is the repo's own and refuses the launch; ours from an earlier attempt is rewritten.</summary>
    /// <param name="workDir">The Step's worktree.</param>
    /// <param name="cancellationToken">Cancels the git query.</param>
    /// <returns>The file's path; an error when the repo has its own hooks file or the files can't be written.</returns>
    internal static async ValueTask<Result<string?>> Write(string workDir, CancellationToken cancellationToken)
    {
        var path = Path.Combine(workDir, RelativePath);
        var ours = Render();
        try
        {
            if (File.Exists(path) && !(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)).AsSpan().SequenceEqual(ours))
            {
                return SindriErrors.RequestInvalid.With($"{path} is the repo's own Codex hooks file, and a skill Step needs that path for its turn hooks (fix: move the repo's hooks to the user-level ~/.codex/hooks.json, or run this skill on Claude Code)");
            }

            if (await StepFactWriter.Excluded(workDir, [RelativePath], cancellationToken).ConfigureAwait(false) is { } excluded)
            {
                return excluded;
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: RelativePath always has a directory part
            OwnerOnly.WriteAtomically(path, ours);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return KernelErrors.Unavailable.With($"the Codex turn hooks could not be written to {path}: {e.Message}");
        }
    }
}
