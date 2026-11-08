using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>The attempt a start runs (engine spec §5.1), the ended attempt it shows instead, and whether the attempt already existed.</summary>
/// <param name="Attempt">The attempt, from 1.</param>
/// <param name="Ended">The ended attempt to show instead of starting, or null.</param>
/// <param name="Existing">Whether the attempt already existed.</param>
internal sealed record AttemptPlan(int Attempt, InstanceAddress? Ended, bool Existing);
