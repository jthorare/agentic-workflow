using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>Runs every sandbox check (GET /sindri/sandbox/check; sandbox spec §6, §8): <c lang="text">sindri sandbox check</c> and the Check again card.</summary>
public sealed record CheckSandboxRequest : IQuery<CheckSandboxResponse>;
