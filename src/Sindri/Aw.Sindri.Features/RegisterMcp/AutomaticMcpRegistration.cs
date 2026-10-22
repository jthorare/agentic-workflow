using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>The registration slice as the setup service and the RegisterMcp job run it (setup-once spec §5.4): Codex's project-trust row is not written, Codex asks the owner once; the terminal verb keeps its behaviour.</summary>
/// <param name="Service">The slice, built with the trust row off.</param>
internal sealed record AutomaticMcpRegistration(IService<RegisterMcpRequest, RegisterMcpResponse> Service);
