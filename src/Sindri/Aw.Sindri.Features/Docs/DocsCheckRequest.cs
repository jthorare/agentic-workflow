using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Docs;

/// <summary>Run the docs check over a docs worktree (docs-sync spec §7.2, API17; 'sindri docs check').</summary>
/// <param name="Workspace">The docs worktree.</param>
/// <param name="Scope">'fact:docsScope': { base, paths (the globs), candidates: [{ path, designNamed }] }.</param>
internal sealed record DocsCheckRequest(string Workspace, JsonObject Scope);
