using Aw.Kernel;

namespace Aw.Context.Core.ConceptScan;

/// <summary>Where the concept hints of a draft are kept (migration 0014). They are keyed by the draft's version number and never touch the map's graph, overrides or pins.</summary>
public interface IConceptHintStore
{
    /// <summary>Stores the hints of a version, replacing any stored before for it.</summary>
    /// <param name="version">The draft's version number.</param>
    /// <param name="hints">The hints (an empty list stores a scan that found nothing).</param>
    /// <param name="scanned">When the scan ended.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> Save(int version, IReadOnlyList<ConceptHint> hints, DateTimeOffset scanned, CancellationToken cancellationToken);

    /// <summary>The hints stored for a version.</summary>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The hints (empty when none were stored), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IReadOnlyList<ConceptHint>>> Read(int version, CancellationToken cancellationToken);
}
