using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>Names the derived concepts with a model, in one call per build. A host with no model gets <see cref="ConceptNames.None"/>, never a failure.</summary>
public interface IConceptNamer
{
    /// <summary>Names the groups.</summary>
    /// <param name="groups">The groups, with their feature and record titles.</param>
    /// <param name="extraPatterns">The profile's 'context.scrub.extraPatterns'.</param>
    /// <param name="maxTokens">The most tokens the call may spend ('context.labeling.maxTokens').</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The names (none when no model answers or the cap would be passed), or an error that is not a missing model.</returns>
    public ValueTask<Result<ConceptNames>> Name(IReadOnlyList<ConceptGroup> groups, IReadOnlyList<ScrubPattern> extraPatterns, int maxTokens, CancellationToken cancellationToken);
}
