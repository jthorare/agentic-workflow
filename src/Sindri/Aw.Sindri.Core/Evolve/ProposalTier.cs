namespace Aw.Sindri.Core.Evolve;

/// <summary>How a proposal is adopted, from what its files touch. The order is strictness: a later value is stricter.</summary>
internal enum ProposalTier
{
    /// <summary>A normal change inside the files its target owns, built and merged as an ordinary work item.</summary>
    Code,

    /// <summary>The owner approves it first: it touches eval or gate machinery, files outside its target, or a path that doesn't normalize.</summary>
    Approval,
}
