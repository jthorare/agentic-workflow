namespace Aw.Sindri.Core.Events;

/// <summary>
/// How a Jev pick's answers were taken (<see cref="ContextModelCalled.How"/>). A label pick fills <paramref name="Jev"/>, <paramref name="FirstCandidate"/> and <paramref name="FileName"/>;
/// a product-surface pick fills <paramref name="Merged"/>, <paramref name="Kept"/>, <paramref name="Rejected"/> and <paramref name="Undecided"/>. The rest stay null.
/// </summary>
/// <param name="Jev">Clusters labeled by Jev's choice.</param>
/// <param name="FirstCandidate">Clusters that took the model's first candidate.</param>
/// <param name="FileName">Clusters that kept the file-derived name.</param>
/// <param name="Merged">Candidate groups merged.</param>
/// <param name="Kept">Candidate groups kept, whether chosen or undecided.</param>
/// <param name="Rejected">Candidate groups rejected.</param>
/// <param name="Undecided">The part of <paramref name="Kept"/> that Jev did not settle.</param>
public sealed record ModelCallHow(int? Jev = null, int? FirstCandidate = null, int? FileName = null, int? Merged = null, int? Kept = null, int? Rejected = null, int? Undecided = null);
