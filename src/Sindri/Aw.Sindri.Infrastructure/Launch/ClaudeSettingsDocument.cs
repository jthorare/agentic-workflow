namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The settings file the launcher writes for Claude Code's 'claude-settings-file' policy form: only a permissions block, so it can only tighten the profile.</summary>
/// <param name="Permissions">The permissions block.</param>
internal sealed record ClaudeSettingsDocument(ClaudePermissions Permissions);
