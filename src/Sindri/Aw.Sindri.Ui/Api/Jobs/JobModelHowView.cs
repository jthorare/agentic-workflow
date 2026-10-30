namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>How a Jev pick's answers were taken: a label pick fills the first three, a product-surface pick the rest.</summary>
/// <param name="Jev">Clusters labeled by Jev's choice.</param>
/// <param name="FirstCandidate">Clusters that took the model's first candidate.</param>
/// <param name="FileName">Clusters that kept the file-derived name.</param>
/// <param name="Merged">Candidate groups merged.</param>
/// <param name="Kept">Candidate groups kept.</param>
/// <param name="Rejected">Candidate groups rejected.</param>
/// <param name="Undecided">The part of Kept that Jev did not settle.</param>
internal sealed record JobModelHowView(int? Jev = null, int? FirstCandidate = null, int? FileName = null, int? Merged = null, int? Kept = null, int? Rejected = null, int? Undecided = null);
