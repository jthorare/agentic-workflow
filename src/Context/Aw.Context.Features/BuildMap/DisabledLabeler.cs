using Aw.Context.Core.Extract;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The labeler a host without a labeling pass gets: a profile that has 'context.labeling' on with nothing set up gets a pass with no labels and the SND-CONTEXT-042 note, never a failed build.</summary>
internal sealed class DisabledLabeler : ILabeler
{
    private const string NoModel = $"labeling is on but this host has no model to ask (fix: {LabelPass.NoModelFix})";

    public ValueTask<Result<LabelPass>> Label(IReadOnlyList<FileCluster> clusters, IReadOnlyList<ScrubPattern> extraPatterns, int maxTokens, LabelOptions? options, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(new LabelPass([], Complete: false, Unavailable: NoModel)));
}
