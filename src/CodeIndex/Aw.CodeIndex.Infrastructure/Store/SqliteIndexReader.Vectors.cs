using System.Globalization;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexReader
{
    // Paths per IN list: far under SQLite's bound-parameter limit, so a 2,000-path read is ten statements.
    private const int VectorPathBatch = 200;

    private const string VectorsHead = "SELECT s.ref, s.kind, s.name, s.file, e.vector FROM symbols s JOIN embeddings e ON e.symbol_id = s.id WHERE e.model = $model AND s.file IN (";

    private const string VectorsTail = ") ORDER BY s.file, s.start_line, s.id";

    public ValueTask<Result<IReadOnlyList<SymbolVector>>> SymbolVectors(IReadOnlyList<string> paths, string model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(model);
        return SqliteFailure.Guard("Reading symbol vectors", async () => await ReadVectors(paths, model, cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<Result<IReadOnlyList<SymbolVector>>> ReadVectors(IReadOnlyList<string> paths, string model, CancellationToken cancellationToken)
    {
        // Batches follow path order, so the rows of batch n all sort before those of batch n + 1.
        var ordered = paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var vectors = new List<SymbolVector>();
        foreach (var batch in ordered.Chunk(VectorPathBatch))
        {
            var command = VectorsCommand(batch, model);
            await using (command.ConfigureAwait(false))
            {
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        vectors.Add(ReadVector(reader));
                    }
                }
            }
        }

        return vectors;
    }

    private SqliteCommand VectorsCommand(string[] batch, string model)
    {
        var names = batch.Select((_, i) => "$p" + i.ToString(CultureInfo.InvariantCulture)).ToList();
        var command = Sql.Command(connection, VectorsHead + string.Join(", ", names) + VectorsTail).With("$model", model);
        for (var i = 0; i < batch.Length; i++)
        {
            _ = command.With(names[i], batch[i]);
        }

        return command;
    }

    private static SymbolVector ReadVector(SqliteDataReader reader)
        => new(
            SymbolRef.TryFrom(reader.GetString(0), out var symbol) ? symbol : throw new InvalidDataException("a stored symbol ref is invalid"),
            reader.GetString(3),
            reader.GetString(2),
            StoredNames.SymbolKindOf(reader.GetString(1)),
            VectorCodec.Decode((byte[])reader.GetValue(4)));
}
