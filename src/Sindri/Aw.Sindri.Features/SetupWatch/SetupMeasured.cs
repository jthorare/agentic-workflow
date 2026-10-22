namespace Aw.Sindri.Features.SetupWatch;

/// <summary>What one look measured: <paramref name="Done"/> is the time from the first 'setup.step.changed' to the one that finished every step, <paramref name="FirstItem"/> the time from there to the first work item's first start. Null for what was not measured now.</summary>
/// <param name="Done">Time to done, once.</param>
/// <param name="FirstItem">Time to first value, once.</param>
internal sealed record SetupMeasured(TimeSpan? Done, TimeSpan? FirstItem);
