using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexBuild
{
    private const string WithoutBodiesOrVectors = "SELECT DISTINCT s.file FROM symbols s "
        + "LEFT JOIN bodies b ON b.symbol_id = s.id LEFT JOIN embeddings e ON e.symbol_id = s.id "
        + "WHERE b.symbol_id IS NULL AND e.symbol_id IS NULL";

    public ValueTask<Result<IReadOnlySet<string>>> FilesWithoutBodiesOrVectors(CancellationToken cancellationToken)
        => SqliteFailure.Guard("Reading the files without bodies", async () => await ReadWithoutBodiesOrVectors(cancellationToken).ConfigureAwait(false));

    private async Task<Result<IReadOnlySet<string>>> ReadWithoutBodiesOrVectors(CancellationToken cancellationToken)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var command = Sql.Command(connection, WithoutBodiesOrVectors);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    _ = paths.Add(reader.GetString(0));
                }
            }
        }

        return paths;
    }
}
