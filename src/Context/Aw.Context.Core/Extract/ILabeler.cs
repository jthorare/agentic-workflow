using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Core.Extract;

/// <summary>Names clusters with a model (the optional labeling pass; on unless the profile turns it off). Over its token cap it answers SND-CONTEXT-042.</summary>
public interface ILabeler
{
    /// <summary>Names the clusters.</summary>
    /// <param name="clusters">The clusters.</param>
    /// <param name="extraPatterns">The profile's 'context.scrub.extraPatterns': the owner's own identifiers, scrubbed from every path before it leaves the box.</param>
    /// <param name="maxTokens">The most tokens (sent and received) the pass may spend ('context.labeling.maxTokens').</param>
    /// <param name="options">The pick and candidate settings of the profile in hand; null labels one name per cluster with no pick.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>The labels the model gave (a model error after some batches answered keeps those, marked incomplete), or the error when none did, or SND-CONTEXT-042 over the cap.</returns>
    public ValueTask<Result<LabelPass>> Label(IReadOnlyList<FileCluster> clusters, IReadOnlyList<ScrubPattern> extraPatterns, int maxTokens, LabelOptions? options, CancellationToken cancellationToken);
}
