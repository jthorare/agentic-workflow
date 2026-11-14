namespace Aw.Sindri.Features.GetOpenRuns;

/// <summary>The open set.</summary>
/// <param name="Runs">Each open run, session or job; empty when a restart would not wait.</param>
internal sealed record GetOpenRunsResponse(IReadOnlyList<OpenRunLine> Runs);
