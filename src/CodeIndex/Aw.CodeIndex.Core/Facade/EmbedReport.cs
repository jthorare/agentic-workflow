using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>Where a repo's embeddings stand after an embed run (spec §5.7a): counts and words only.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Embedded">How many symbols have a vector.</param>
/// <param name="Embeddable">How many symbols should have one.</param>
/// <param name="Carried">Of the symbols with a vector, how many already had one when the run began (carried over from the previous index).</param>
/// <param name="Made">How many vectors this run made; a run that re-embeds everything shows a Made as large as Embeddable.</param>
/// <param name="Embeddings">The embeddings layer's state: ok when every symbol has a vector, pending while some remain.</param>
public sealed record EmbedReport(RepoName Repo, int Embedded, int Embeddable, int Carried, int Made, LayerState Embeddings);
