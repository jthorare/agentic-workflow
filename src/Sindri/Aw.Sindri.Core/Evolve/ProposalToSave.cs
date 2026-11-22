namespace Aw.Sindri.Core.Evolve;

/// <summary>A new proposal as code hands it to the store: the model's original plus what code recorded about it.</summary>
/// <param name="Original">The proposal as the model drafted it; stays local.</param>
/// <param name="Tier">Its tier, from <see cref="ProposalTiers"/>.</param>
/// <param name="Sources">The turns the model saw for it, by session; recorded by code, never read from the model's answer.</param>
/// <param name="Origin">Where it came from, such as 'workflow:steering-miner run:&lt;run&gt;'.</param>
/// <param name="Draft">Where its real edit lives, or null when there is none; a merge keeps the newer non-null one.</param>
public sealed record ProposalToSave(Proposal Original, ProposalTier Tier, IReadOnlyList<LeakSource> Sources, string Origin, ProposalDraft? Draft = null);
