using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.SearchMemory;

/// <summary>
/// Recalls facts. Unexpired facts only. It embeds the query and ranks by cosine at the level's floor; when the embedder says Unavailable it ranks by keywords
/// and answers mode 'keyword' with the reason. An unknown memory is SND-MEM-001 and a bad level SND-MEM-005.
/// </summary>
/// <param name="store">The memories.</param>
/// <param name="embedder">Embeds the query.</param>
/// <param name="time">The clock, for expiry.</param>
internal sealed class SearchMemoryService(IMemoryStore store, IMemoryEmbedder embedder, TimeProvider time) : IService<SearchMemoryRequest, SearchMemoryResponse>
{
    private readonly IMemoryStore _store = store;
    private readonly IMemoryEmbedder _embedder = embedder;
    private readonly TimeProvider _time = time;

    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    public async ValueTask<Result<SearchMemoryResponse>> Handle(SearchMemoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var level = MatchLevels.Parse(request.Level);
        if (level.IsError)
        {
            return level.Error;
        }

        var name = MemoryName.Parse(request.Name);
        if (name.IsError)
        {
            return name.Error;
        }

        var stored = await _store.Facts(name.Value, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var now = _time.GetUtcNow();
        var live = stored.Value.Where(f => !f.IsExpired(now)).ToList();
        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);
        var embedded = await _embedder.Embed([request.Query], cancellationToken).ConfigureAwait(false);
        if (embedded.Vectors is { Count: 1 } vectors)
        {
            var recall = MemoryRecall.Semantic(vectors[0], live, level.Value, limit);
            return Answer(recall, recall.Skipped > 0 ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{recall.Skipped} facts were not compared: they have no vector yet or were embedded with another model") : null);
        }

        var reason = embedded.Reason is { Length: > 0 } why ? $"{SindriErrors.MemoryEmbedUnavailable.Title} ({why})" : SindriErrors.MemoryEmbedUnavailable.Title;
        return Answer(KeywordRank.Rank(request.Query, live, limit), reason);
    }

    private static SearchMemoryResponse Answer(MemoryRecall recall, string? note) =>
        new([.. recall.Facts.Select(r => MemoryFactView.From(r.Fact, r.Score))], recall.Facts.Count, recall.Mode == RecallMode.Semantic ? "semantic" : "keyword", note);
}
