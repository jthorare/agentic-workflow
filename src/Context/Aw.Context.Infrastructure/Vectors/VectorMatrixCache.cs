using Aw.Context.Core.Records;
using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Vectors;

/// <summary>
/// The append-only RAM copy of the chunk vectors of one model (spec §4.4). It remembers the highest 'Seq' it has read and
/// loads only rows after it; it starts over when 'store_epoch.delete_epoch' moved (a purge, a tombstone, a re-chunk or a
/// re-embed) or the model changed. Memory only, never written to disk.
/// </summary>
internal sealed class VectorMatrixCache(IRecordStore store) : IVectorMatrixCache, IDisposable
{
    private const int PageSize = 10_000;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _model;
    private long _deleteEpoch = -1;
    private long _recordStamp = -1;
    private long _lastSeq;
    private int _dim;
    private int _rows;
    private float[] _data = [];
    private string[] _keys = [];

    public void Dispose() => _gate.Dispose();

    public async ValueTask<Result<VectorMatrix>> Current(string model, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(model);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Stamps first: a vector that lands after this read has a higher Seq and the next call sees the stamp move.
            var stamps = await store.Stamps(cancellationToken).ConfigureAwait(false);
            if (stamps.IsError)
            {
                return stamps.Error;
            }

            if (_model != model || _deleteEpoch != stamps.Value.DeleteEpoch)
            {
                Reset(model, stamps.Value.DeleteEpoch);
            }

            if (_recordStamp != stamps.Value.RecordStamp)
            {
                var loaded = await Load(model, cancellationToken).ConfigureAwait(false);
                if (loaded.IsError)
                {
                    return loaded.Error;
                }

                _recordStamp = stamps.Value.RecordStamp;
            }

            return Snapshot();
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private void Reset(string model, long deleteEpoch)
    {
        _model = model;
        _deleteEpoch = deleteEpoch;
        _recordStamp = -1;
        _lastSeq = 0;
        _dim = 0;
        _rows = 0;
        _data = [];
        _keys = [];
    }

    private async ValueTask<Result<Unit>> Load(string model, CancellationToken cancellationToken)
    {
        while (true)
        {
            var page = await store.Vectors(VectorSpace.Chunk, _lastSeq, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var row in page.Value)
            {
                _lastSeq = row.Seq;
                if (row.Vector.Model == model)
                {
                    Append(row.Vector);
                }
            }

            if (page.Value.Count < PageSize)
            {
                return Unit.Value;
            }
        }
    }

    // A row of another dimension than the first is left out: one matrix has one dimension, and a model has one.
    private void Append(VectorWrite vector)
    {
        var vec = vector.Vec.Span;
        if (vec.Length == 0 || (_rows > 0 && vec.Length != _dim))
        {
            return;
        }

        _dim = vec.Length;
        if ((_rows + 1) * _dim > _data.Length)
        {
            var capacity = Math.Max(_rows * 2, 1024);
            Array.Resize(ref _data, capacity * _dim);
            Array.Resize(ref _keys, capacity);
        }

        vec.CopyTo(_data.AsSpan(_rows * _dim, _dim));
        _keys[_rows++] = vector.Key;
    }

    // The arrays only grow by copy and the prefix is never rewritten, so a snapshot stays valid while later calls append.
    private VectorMatrix Snapshot() => new(_dim, _data.AsMemory(0, _rows * _dim), new KeyPrefix(_keys, _rows));

    private sealed class KeyPrefix(string[] keys, int count) : IReadOnlyList<string>
    {
        public int Count => count;

        public string this[int index] => index < count ? keys[index] : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<string> GetEnumerator() => keys.Take(count).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
