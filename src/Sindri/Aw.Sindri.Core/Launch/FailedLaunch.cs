using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>The session did not start.</summary>
/// <param name="Error">Why: a catalogue code (SND-LAUNCH-…).</param>
/// <param name="Stage">Where it stopped.</param>
public sealed record FailedLaunch(Error Error, LaunchStage Stage) : LaunchResult;
