using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.WriteMemoryFact;

/// <summary>
/// Writes a fact. The text is checked (SND-MEM-003), scrubbed and then embedded (a missing model stores the fact with no vector). A fact whose vector is at or above
/// <see cref="MemoryDedupe.Threshold"/> of an unexpired fact with the same tags refreshes that fact (action 'refreshed', one row); otherwise it is added.
/// </summary>
/// <param name="store">The memories.</param>
/// <param name="embedder">Embeds the scrubbed text.</param>
/// <param name="scrubber">Redacts the text before anything stores it.</param>
/// <param name="time">The clock.</param>
/// <param name="ids">The id source for a new fact's key.</param>
internal sealed class WriteMemoryFactService(IMemoryStore store, IMemoryEmbedder embedder, ITextScrubber scrubber, TimeProvider time, IIdGenerator ids) : IService<WriteMemoryFactRequest, WriteMemoryFactResponse>
{
    private readonly IMemoryStore _store = store;
    private readonly IMemoryEmbedder _embedder = embedder;
    private readonly ITextScrubber _scrubber = scrubber;
    private readonly TimeProvider _time = time;
    private readonly IIdGenerator _ids = ids;

    public async ValueTask<Result<WriteMemoryFactResponse>> Handle(WriteMemoryFactRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var checkedText = MemoryFact.CheckText(request.Text);
        if (checkedText.IsError)
        {
            return checkedText.Error;
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

        var text = _scrubber.Scrub(checkedText.Value);
        var tags = Tags(request.Tags);
        var now = _time.GetUtcNow();
        var embedded = await _embedder.Embed([text], cancellationToken).ConfigureAwait(false);
        var vector = embedded.Vectors is { Count: 1 } vectors ? vectors[0] : ReadOnlyMemory<float>.Empty;
        var duplicate = vector.IsEmpty ? null : MemoryDedupe.FindDuplicate(vector, tags, [.. stored.Value.Where(f => !f.IsExpired(now))]);
        if (duplicate is not null)
        {
            var refreshed = await _store.Refresh(name.Value, duplicate.Key, now, request.ExpiresAt, cancellationToken).ConfigureAwait(false);
            return refreshed.IsError ? refreshed.Error : new WriteMemoryFactResponse(duplicate.Key, "refreshed");
        }

        var key = "mf_" + _ids.NewId().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        var fact = new MemoryFact(key, name.Value, text, tags, request.Provenance ?? MemoryProvenance.None, request.ExpiresAt, now, now, vector);
        var added = await _store.Add(fact, cancellationToken).ConfigureAwait(false);
        return added.IsError ? added.Error : new WriteMemoryFactResponse(key, "added");
    }

    // Tags are lowercase, trimmed, sorted and without repeats, so the same set is the same identity.
    private static string[] Tags(IReadOnlyList<string>? tags) =>
        tags is null ? [] : [.. tags.Select(static t => t.Trim().ToLowerInvariant()).Where(static t => t.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
