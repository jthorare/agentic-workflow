namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Where a session's MCP calls go: Sindri's loopback port and the repo the session's tools are scoped to.</summary>
/// <param name="Repo">The profile repo name ('X-Sindri-Repo').</param>
/// <param name="Port">Sindri's API port.</param>
/// <param name="Instance">The workflow instance address of an agent Step's launch ('X-Sindri-Instance'), or null for a plain session.</param>
/// <param name="Run">The Step run's token ('X-Sindri-Run'), or null for a plain session.</param>
internal sealed record McpTarget(string Repo, int Port, string? Instance = null, string? Run = null);
