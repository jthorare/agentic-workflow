using Aw.Context.Core.Docs;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.QueryContext;

/// <summary>What the live query answers from: the store's map, the profile's sources and the repo reader of the diff shape.</summary>
/// <param name="Graphs">The map reader.</param>
/// <param name="Catalog">The source catalog.</param>
/// <param name="Repos">The repo reader.</param>
internal sealed record LiveInputs(MapReader Graphs, ISourceCatalog Catalog, IRepoReader Repos)
{
    /// <summary>The inputs of one live query.</summary>
    /// <returns>The pipeline's inputs.</returns>
    internal PipelineInputs ToInputs() => new(new LiveMap(Graphs), Catalog, Diff: new DiffAnswer(Graphs, Repos));
}
