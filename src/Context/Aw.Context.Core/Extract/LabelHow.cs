namespace Aw.Context.Core.Extract;

/// <summary>How the labels of a pass with the Jev pick were chosen (spec §6.5): counts of clusters, which feed the call's ledger event.</summary>
/// <param name="Jev">Clusters whose label Jev chose.</param>
/// <param name="FirstCandidate">Clusters that took the model's first candidate: Jev didn't decide, wasn't asked, or the model gave only that one.</param>
/// <param name="FileName">Clusters that kept the file-derived name because the model gave no candidate.</param>
public sealed record LabelHow(int Jev, int FirstCandidate, int FileName);
