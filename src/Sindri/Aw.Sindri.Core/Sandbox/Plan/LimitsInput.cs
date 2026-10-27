using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>What <see cref="LimitsResolver"/> resolves from.</summary>
/// <param name="Limits">The profile's 'sandbox.limits', or null.</param>
/// <param name="AttendedSettings">The profile's 'sandbox.attended', or null.</param>
/// <param name="Attended">True for a session the owner opened.</param>
/// <param name="StepTimeout">The Step's timeout.</param>
/// <param name="LongMax">'workflow.steps.longMax'.</param>
/// <param name="RamBytes">The box's RAM.</param>
/// <param name="Cores">The number of cores.</param>
public sealed record LimitsInput(SandboxLimits? Limits, SandboxAttended? AttendedSettings, bool Attended, TimeSpan? StepTimeout, TimeSpan? LongMax, long RamBytes, int Cores);
