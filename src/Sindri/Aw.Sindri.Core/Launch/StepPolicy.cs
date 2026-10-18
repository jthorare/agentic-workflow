using System.Text.Json.Serialization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// A Step's policy as the engine states it from the recipe's W9 (launcher L-17(a), amended by the owner 2026-10-17): target-neutral, so the
/// engine writes no policy file. The launcher renders it for the entry's <see cref="PolicyForm"/>: 'claude-settings-file' becomes a 0600
/// settings file the launcher writes and deletes with the launch, 'codex-overrides' becomes typed '-c' overrides, 'cursor-config' is
/// reserved (SND-LAUNCH-013). A Step only tightens the profile, and a mode the entry can't express is SND-LAUNCH-013.
/// </summary>
/// <param name="AllowedTools">The tools the Step may use; empty means the CLI's read-only defaults for the mode. Codex has no per-tool form, so for a 'codex-overrides' entry the list is ignored.</param>
/// <param name="Mode">The permission mode; never bypass or full access. For Codex, ReadOnly with a <paramref name="WritablePath"/> runs as 'workspace-write' limited to that one path (Codex has no read-only mode with a writable root).</param>
/// <param name="WritablePath">The one path the Step may write (its worktree or Step directory), or null for none.</param>
/// <param name="LoginSource">
/// The model login source the Step asks for (sandbox spec SB18, owner note 4): a stricter one than the profile gives, never a looser.
/// Null means no ask and is left out of the JSON. The launcher takes the stricter of the ask and the profile, repo and target value (SB-1/SB-5a).
/// </param>
public sealed record StepPolicy(
    IReadOnlyList<string> AllowedTools,
    StepPermissionMode Mode,
    string? WritablePath,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ModelLoginSource? LoginSource = null);
