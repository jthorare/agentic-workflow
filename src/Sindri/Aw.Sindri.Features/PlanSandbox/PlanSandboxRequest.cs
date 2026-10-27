using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.PlanSandbox;

/// <summary>Builds the sandbox plan of one launch through the registered <see cref="ISandbox"/> (sandbox spec SB12). The launcher (SB-5a) and 'sindri sandbox show' (SB-5a) call it.</summary>
/// <param name="Launch">The launch to plan.</param>
public sealed record PlanSandboxRequest(SandboxRequest Launch);
