using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>
/// Register Sindri's MCP address in a repo from the app (setup-once spec §5.4, POST /setup/mcp; a class Job action like AddRepo, so a session is refused): a job keyed by 'mcp-' and the first 16 hex of the sha256 of the repo's name.
/// The cheap check (the approved profile names the repo, SND-SETUP-004) runs before the job exists.
/// </summary>
/// <param name="Repo">The repo's name; null takes the approved profile's only repo.</param>
public sealed record RegisterMcpJobRequest(string? Repo) : IJob<RegisterMcpResponse>;
