namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>An evidence provider block (spec §2, A §17.11). Registered with AddEvidence.</summary>
public interface IEvidenceProvider
{
    /// <summary>Collects the evidence.</summary>
    /// <param name="context">What to collect.</param>
    /// <param name="cancellationToken">Cancels the collection.</param>
    /// <returns>Whether it passes.</returns>
    public ValueTask<EvidenceResult> Collect(EvidenceContext context, CancellationToken cancellationToken);
}
