using Aw.Context.Core.Concepts;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The namer a host without a labeling pass gets: no names, so every concept keeps its deterministic name (SND-CONTEXT-042 is a note, never a failed build).</summary>
internal sealed class DisabledConceptNamer : IConceptNamer
{
    public ValueTask<Result<ConceptNames>> Name(IReadOnlyList<ConceptGroup> groups, IReadOnlyList<ScrubPattern> extraPatterns, int maxTokens, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(ConceptNames.None));
}
