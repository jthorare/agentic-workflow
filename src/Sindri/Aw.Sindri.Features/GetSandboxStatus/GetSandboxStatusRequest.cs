using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetSandboxStatus;

/// <summary>Reads what the sandbox looked like at the last check (GET /sindri/sandbox/status; sandbox spec §8): cheap, it runs no probe.</summary>
public sealed record GetSandboxStatusRequest : IQuery<SandboxStatusResponse>;
