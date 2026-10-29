namespace Aw.Sindri.Core.Jobs;

/// <summary>How many of a job's model calls one harness and model answered (<see cref="ModelTotals.AnsweredBy"/>).</summary>
/// <param name="Harness">The harness.</param>
/// <param name="Model">The model.</param>
/// <param name="Count">The calls it answered.</param>
public sealed record ModelAnsweredCount(string Harness, string Model, int Count);
