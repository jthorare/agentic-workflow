using Aw.CodeIndex.Core.Model;
using Aw.Kernel;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexBuild
{
    private const string Candidates = "SELECT s.ref, s.name, s.signature, b.text "
        + "FROM bodies b JOIN symbols s ON s.id = b.symbol_id LEFT JOIN embeddings e ON e.symbol_id = s.id "
        + "WHERE e.symbol_id IS NULL "
        + "ORDER BY s.ref LIMIT $limit";

    private const string InsertVector = "INSERT OR REPLACE INTO embeddings(symbol_id, model, vector) SELECT id, $model, $vector FROM symbols WHERE ref = $ref";

    private const string DropBody = "DELETE FROM bodies WHERE symbol_id = (SELECT id FROM symbols WHERE ref = $ref)";

    public ValueTask<Result<Unit>> ReplaceGraph(GraphSnapshot graph, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return SqliteFailure.Guard("Replacing the graph", async () => await WriteGraph(graph, cancellationToken).ConfigureAwait(false));
    }

    public ValueTask<Result<IReadOnlyList<EmbedCandidate>>> EmbedCandidates(int limit, CancellationToken cancellationToken)
        => SqliteFailure.Guard("Reading the symbols to embed", async () => await ReadCandidates(limit, cancellationToken).ConfigureAwait(false));

    public ValueTask<Result<Unit>> WriteEmbeddings(IReadOnlyList<EmbeddingRecord> vectors, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        return SqliteFailure.Guard("Writing embeddings", async () => await InsertVectors(vectors, cancellationToken).ConfigureAwait(false));
    }

    public ValueTask<Result<EmbeddingProgress>> CountEmbeddings(CancellationToken cancellationToken)
        => SqliteFailure.Guard<EmbeddingProgress>("Counting embeddings", async () => new EmbeddingProgress(
            (int)await Sql.Count(connection, "SELECT COUNT(*) FROM embeddings", cancellationToken).ConfigureAwait(false),
            (int)await Sql.Count(connection, "SELECT COUNT(*) FROM symbols s WHERE EXISTS (SELECT 1 FROM bodies b WHERE b.symbol_id = s.id) OR EXISTS (SELECT 1 FROM embeddings e WHERE e.symbol_id = s.id)", cancellationToken).ConfigureAwait(false)));

    private async Task<Result<Unit>> WriteGraph(GraphSnapshot graph, CancellationToken cancellationToken)
    {
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            _ = await Sql.Run(connection, "DELETE FROM graph_edges", cancellationToken, transaction).ConfigureAwait(false);
            _ = await Sql.Run(connection, "DELETE FROM graph_nodes", cancellationToken, transaction).ConfigureAwait(false);
            var node = new Stmt(connection, transaction, "INSERT OR REPLACE INTO graph_nodes(id, file, name, line, community) VALUES ($id, $file, $name, $line, $community)", "$id", "$file", "$name", "$line", "$community");
            await using (node.ConfigureAwait(false))
            {
                foreach (var n in graph.Nodes)
                {
                    _ = await node.Set("$id", n.NodeKey).Set("$file", n.File).Set("$name", n.Name).Set("$line", n.Line).Set("$community", n.Community).Run(cancellationToken).ConfigureAwait(false);
                }
            }

            var edge = new Stmt(connection, transaction, "INSERT INTO graph_edges(src, dst, relation, confidence) VALUES ($src, $dst, $relation, $confidence)", "$src", "$dst", "$relation", "$confidence");
            await using (edge.ConfigureAwait(false))
            {
                foreach (var e in graph.Edges)
                {
                    _ = await edge.Set("$src", e.Source).Set("$dst", e.Target).Set("$relation", e.Relation).Set("$confidence", e.Confidence).Run(cancellationToken).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }

    private async Task<Result<IReadOnlyList<EmbedCandidate>>> ReadCandidates(int limit, CancellationToken cancellationToken)
    {
        var found = new List<EmbedCandidate>();
        var command = Sql.Command(connection, Candidates).With("$limit", Math.Max(limit, 0));
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    found.Add(new EmbedCandidate(
                        Core.Values.SymbolRef.TryFrom(reader.GetString(0), out var symbol) ? symbol : throw new InvalidDataException("a stored symbol ref is invalid"),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3)));
                }
            }
        }

        return found;
    }

    private async Task<Result<Unit>> InsertVectors(IReadOnlyList<EmbeddingRecord> vectors, CancellationToken cancellationToken)
    {
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var insert = new Stmt(connection, transaction, InsertVector, "$ref", "$model", "$vector");
            await using (insert.ConfigureAwait(false))
            {
                var drop = new Stmt(connection, transaction, DropBody, "$ref");
                await using (drop.ConfigureAwait(false))
                {
                    foreach (var vector in vectors)
                    {
                        // The body goes only when the vector was stored, so a ref that names no symbol changes nothing.
                        var stored = await insert.Set("$ref", vector.Symbol.Value).Set("$model", vector.Model).Set("$vector", VectorCodec.Encode(vector.Vector.Span)).Run(cancellationToken).ConfigureAwait(false);
                        if (stored > 0)
                        {
                            _ = await drop.Set("$ref", vector.Symbol.Value).Run(cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}
