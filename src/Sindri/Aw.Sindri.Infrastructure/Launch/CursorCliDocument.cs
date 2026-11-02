namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The per-launch <c lang="text">.cursor/cli.json</c> the launcher writes for the 'cursor-config' policy form: a permissions block, so it can only tighten the profile.</summary>
/// <param name="Permissions">The permissions block.</param>
internal sealed record CursorCliDocument(CursorPermissions Permissions);
