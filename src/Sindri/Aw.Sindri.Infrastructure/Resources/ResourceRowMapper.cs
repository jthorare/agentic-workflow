using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>Maps a row of <see cref="ResourceProvisioner"/>'s select.</summary>
internal sealed class ResourceRowMapper : IRowMapper<ResourceRow>
{
    public static readonly ResourceRowMapper Instance = new();

    public ResourceRow Map(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetInt64(4));
}
