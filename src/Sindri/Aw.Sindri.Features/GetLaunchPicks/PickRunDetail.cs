namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>The part of a workflow event's detail the picks read.</summary>
/// <param name="FailureKind">On a step.failed row of an agent run: refused, check_failed or crashed.</param>
/// <param name="Delta">The post-state delta.</param>
public sealed record PickRunDetail(string? FailureKind = null, PickRunDelta? Delta = null);
