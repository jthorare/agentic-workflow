using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexReader
{
    // A band shared by thousands of boilerplate symbols is cut here; Core ranks what is left.
    private const int MaxCloneCandidates = 1_000;

    private const string Subject = "SELECT id, shape_hash, minhash FROM symbols WHERE ref = $ref";

    private const string SameShape = "SELECT " + HitColumns + " FROM symbols s WHERE s.shape_hash = $shape AND s.id <> $id AND s.minhash IS NOT NULL ORDER BY s.ref LIMIT $limit";

    private const string SharedBand = "SELECT " + HitColumns + ", s.minhash FROM symbols s WHERE s.minhash IS NOT NULL AND s.id <> $id AND s.id IN ("
        + "SELECT b2.symbol_id FROM bands b1 JOIN bands b2 ON b2.key = b1.key WHERE b1.symbol_id = $id) ORDER BY s.ref LIMIT $limit";

    private const string Vectors = "SELECT symbol_id, vector FROM embeddings";

    private const string HitOf = "SELECT " + HitColumns + " FROM symbols s WHERE s.id = $id";

    public ValueTask<Result<CloneCandidates>> CloneCandidates(SymbolRef symbol, CancellationToken cancellationToken)
        => SqliteFailure.Guard("Finding clone candidates", async () => await FindClones(symbol, cancellationToken).ConfigureAwait(false));

    public ValueTask<Result<IReadOnlyList<Neighbor>>> Nearest(ReadOnlyMemory<float> vector, int k, CancellationToken cancellationToken)
        => SqliteFailure.Guard("Finding the nearest vectors", async () => await FindNearest(vector, k, cancellationToken).ConfigureAwait(false));

    private async ValueTask<Result<CloneCandidates>> FindClones(SymbolRef symbol, CancellationToken cancellationToken)
    {
        var found = await ReadSubject(symbol, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return CodeIndexErrors.SymbolNotFound.With($"No symbol has the ref '{symbol.Value}'.");
        }

        var (id, shape, minhash) = found.Value;
        if (minhash is null)
        {
            return new CloneCandidates([], ReadOnlyMemory<byte>.Empty, []);
        }

        var exact = Sql.Command(connection, SameShape).With("$shape", shape).With("$id", id).With("$limit", MaxCloneCandidates);
        await using (exact.ConfigureAwait(false))
        {
            var banded = Sql.Command(connection, SharedBand).With("$id", id).With("$limit", MaxCloneCandidates);
            await using (banded.ConfigureAwait(false))
            {
                return new CloneCandidates(
                    await ReadHits(exact, cancellationToken).ConfigureAwait(false),
                    minhash,
                    await ReadBanded(banded, cancellationToken).ConfigureAwait(false));
            }
        }
    }

    private async Task<(long Id, string Shape, byte[]? MinHash)?> ReadSubject(SymbolRef symbol, CancellationToken cancellationToken)
    {
        var command = Sql.Command(connection, Subject).With("$ref", symbol.Value);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                    ? (reader.GetInt64(0), reader.GetString(1), Sql.Blob(reader, 2))
                    : null;
            }
        }
    }

    private static async Task<List<MinHashCandidate>> ReadBanded(SqliteCommand command, CancellationToken cancellationToken)
    {
        var found = new List<MinHashCandidate>();
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                found.Add(new MinHashCandidate(ReadHit(reader, 0), (byte[])reader.GetValue(9)));
            }
        }

        return found;
    }

    private async ValueTask<Result<IReadOnlyList<Neighbor>>> FindNearest(ReadOnlyMemory<float> vector, int k, CancellationToken cancellationToken)
    {
        var take = k;
        var best = new List<(long Id, double Score)>();
        if (take > 0)
        {
            var command = Sql.Command(connection, Vectors);
            await using (command.ConfigureAwait(false))
            {
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        Keep(best, take, (reader.GetInt64(0), VectorCodec.Cosine(vector.Span, VectorCodec.Decode((byte[])reader.GetValue(1)))));
                    }
                }
            }
        }

        var neighbors = new List<Neighbor>(best.Count);
        foreach (var (id, score) in best)
        {
            var command = Sql.Command(connection, HitOf).With("$id", id);
            await using (command.ConfigureAwait(false))
            {
                neighbors.AddRange((await ReadHits(command, cancellationToken).ConfigureAwait(false)).Select(hit => new Neighbor(hit, score)));
            }
        }

        return neighbors;
    }

    // Keeps the best <paramref name="take"/> scores, best first: a bounded insertion, so a large index never sits in memory.
    private static void Keep(List<(long Id, double Score)> best, int take, (long Id, double Score) candidate)
    {
        var at = best.FindIndex(b => candidate.Score > b.Score);
        if (at < 0 && best.Count < take)
        {
            best.Add(candidate);
        }
        else if (at >= 0)
        {
            best.Insert(at, candidate);
            if (best.Count > take)
            {
                best.RemoveAt(best.Count - 1);
            }
        }
    }
}
