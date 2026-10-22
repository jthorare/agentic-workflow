using Aw.Context.Core.Linking;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;

namespace Aw.Context.Features.QueryContext;

/// <summary>The query (spec §8.2) over the live map and the profile's sources: <see cref="QueryPipeline"/> with the store's map.</summary>
internal sealed class QueryContextService(IScrubberFactory scrubberFactory, IIdGenerator ids, TimeProvider clock, LiveInputs live, IIntentStore intents, IModelPort model)
    : IService<QueryContextRequest, ContextResult>
{
    /// <summary>The most fused items fetched (and so cited) per query.</summary>
    internal const int MaxItems = QueryPipeline.MaxItems;

    public ValueTask<Result<ContextResult>> Handle(QueryContextRequest request, CancellationToken cancellationToken) =>
        new QueryPipeline(scrubberFactory, ids, clock, intents, model).Run(request, live.ToInputs(), cancellationToken);
}
