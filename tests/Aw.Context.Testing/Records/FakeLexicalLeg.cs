using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Search;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>An <see cref="ILexicalLeg"/> over a fixed list of chunks: the scope mask applied the way the store applies it, one chunk per record, in the order they were added.</summary>
public sealed class FakeLexicalLeg : ILexicalLeg
{
    private readonly List<(ChunkKey Chunk, AudienceKey? Audience)> _chunks = [];

    /// <summary>When set, every search fails with it (a store that can't be read).</summary>
    public Error? Failure { get; set; }

    /// <summary>When set, every search answers no hits and this.</summary>
    public LexicalDegraded? Degraded { get; set; }

    /// <summary>The terms of the last search.</summary>
    public IReadOnlyList<string> LastTerms { get; private set; } = [];

    /// <summary>The scope of the last search.</summary>
    public AudienceScope? LastScope { get; private set; }

    /// <summary>Adds a chunk that matches every search.</summary>
    /// <param name="chunk">The chunk.</param>
    /// <param name="audience">The audience of its record; null is no one.</param>
    /// <returns>This leg.</returns>
    public FakeLexicalLeg Add(ChunkKey chunk, AudienceKey? audience)
    {
        _chunks.Add((chunk, audience));
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<LexicalAnswer>> Search(IReadOnlyList<string> terms, AudienceScope scope, int limit, CancellationToken cancellationToken)
    {
        (LastTerms, LastScope) = (terms, scope);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<LexicalAnswer>>(failure);
        }

        var hits = Degraded is not null
            ? []
            : _chunks.Where(c => scope.Allows(c.Audience, c.Chunk.Record)).GroupBy(c => c.Chunk.Record.Value, StringComparer.Ordinal)
                .Select(g => new RecordHit(g.First().Chunk, "fake", LineStart: null, LineEnd: null, Score: 1)).Take(limit).ToList();
        return ValueTask.FromResult<Result<LexicalAnswer>>(new LexicalAnswer(hits, Degraded));
    }
}
