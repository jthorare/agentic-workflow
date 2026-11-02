using Aw.Context.Core.Records;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

internal sealed partial class SqliteRecordStore
{
    private const float NormTolerance = 1e-4f;

    public async ValueTask<Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        var begun = await BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var stored = 0;
            foreach (var vector in vectors.Where(v => v.Vec.Length > 0))
            {
                var done = await StoreVector(begun.Value, vector, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return done.Error;
                }

                stored += done.Value;
            }

            // One stamp per committed embed batch, so the caches reload only what is new (spec §4.4).
            var stamped = stored == 0 ? Results.Success(0) : await Run(begun.Value, RecordSql.BumpRecordStamp, bind: null, cancellationToken: cancellationToken).ConfigureAwait(false);
            var committed = stamped.IsError ? stamped.Error : await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            database.RestrictFiles();
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : stored;
        }
    }

    public async ValueTask<Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 10_000);
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<VectorRow>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var statement = new SqlStatement(RecordSql.ReadVectors, c => c.Bind("@space", RecordWire.Of(space)).Bind("@after", afterSeq).Bind("@limit", limit));
            var rows = await SqliteQueries.List(session, statement, RecordMappers.Vector(space), cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }

    public async ValueTask<Result<IReadOnlyList<ChunkToEmbed>>> ChunksToEmbed(string model, int limit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(model);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 10_000);
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<ChunkToEmbed>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var statement = new SqlStatement(RecordSql.ChunksToEmbed, c => c.Bind("@model", model).Bind("@limit", limit));
            var rows = await SqliteQueries.List(session, statement, RecordMappers.ToEmbed, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }

    public async ValueTask<Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var begun = await BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var removed = 0;
            foreach (var key in keys)
            {
                var done = await Run(begun.Value, RecordSql.DeleteVector, c => c.Bind("@space", RecordWire.Of(space)).Bind("@key", key), cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return done.Error;
                }

                removed += done.Value;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            database.RestrictFiles();
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : removed;
        }
    }

    private static async ValueTask<Result<int>> StoreVector(SqliteUnitOfWork session, VectorWrite vector, CancellationToken cancellationToken)
    {
        var chunk = vector.Space == VectorSpace.Chunk;
        void Bind(Microsoft.Data.Sqlite.SqliteCommand c) => c.Bind("@key", vector.Key).Bind("@model", vector.Model).Bind("@hash", vector.TextHash).Bind("@dim", vector.Vec.Length).Bind("@vec", RecordMappers.Bytes(Normalised(vector.Vec.Span)));
        var replaced = await Run(session, chunk ? RecordSql.ReplaceChunkVector : RecordSql.ReplaceNodeVector, Bind, cancellationToken).ConfigureAwait(false);
        return replaced.IsError ? replaced : await Run(session, chunk ? RecordSql.InsertChunkVector : RecordSql.InsertNodeVector, Bind, cancellationToken).ConfigureAwait(false);
    }

    // Cosine is a dot product only when every stored vector has length 1 (spec §4.4); a vector already within tolerance is stored as it came, bit for bit.
    private static float[] Normalised(ReadOnlySpan<float> vec)
    {
        var sum = 0f;
        foreach (var component in vec)
        {
            sum += component * component;
        }

        var norm = MathF.Sqrt(sum);
        var copy = vec.ToArray();
        if (norm > 0 && MathF.Abs(norm - 1f) > NormTolerance)
        {
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] /= norm;
            }
        }

        return copy;
    }
}
