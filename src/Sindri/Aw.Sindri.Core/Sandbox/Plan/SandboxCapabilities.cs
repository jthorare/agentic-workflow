namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>What <see cref="ISandbox.Probe"/> found on this box.</summary>
/// <param name="Mechanism">The mechanism that can run here.</param>
/// <param name="Detail">One line for 'sandbox check' and doctor (versions, never secrets), or null.</param>
public sealed record SandboxCapabilities(SandboxMechanism Mechanism, string? Detail = null);
