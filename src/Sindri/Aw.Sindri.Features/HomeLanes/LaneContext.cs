namespace Aw.Sindri.Features.HomeLanes;

/// <summary>One lane from 'aw-meta lanes-context --json' (the objects of 'status --json' context[] are the same).</summary>
/// <param name="Lane">The lane name.</param>
/// <param name="Tokens">The context size.</param>
/// <param name="Threshold">The recycle mark ('recycleAt' in the team config).</param>
/// <param name="Verdict">The verdict as aw-meta prints it ('Ok', 'Recycle', ...).</param>
internal sealed record LaneContext(string Lane, long Tokens, long Threshold, string Verdict);
