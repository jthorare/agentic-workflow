namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>A block's own per-run limits (PR 14). <see cref="LimitsResolver"/> clamps each to the profile's 'sandbox.limits' and the box, so an override only tightens.</summary>
/// <param name="Memory">A size such as '2GiB', or null to keep the profile's.</param>
/// <param name="Cpu">A percent share such as '25%', or null to keep the profile's.</param>
/// <param name="StepTimeout">The Step timeout, or null to keep the Step's.</param>
public sealed record ResourceLimitOverride(string? Memory = null, string? Cpu = null, TimeSpan? StepTimeout = null);
