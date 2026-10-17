namespace Aw.Sindri.Core.Launch;

/// <summary>What <see cref="ILaunchProbes.Record"/> found and did.</summary>
/// <param name="Probed">What the launcher found.</param>
/// <param name="Changed">True when 'launch.probed' was appended because the facts differ from the newest probe's.</param>
public sealed record RecordedProbe(LaunchProbed Probed, bool Changed);
