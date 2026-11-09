using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Ledger;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>The 'toolkit' table of the ledger file (migration 17). A ledger that is missing or older than 17 reads as no toolkits.</summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqliteToolkitStore(LedgerDatabase database) : IToolkitStore
{
    private const string Select = "SELECT name, dir, state, source_hash, built_at, manifest_json, created_by FROM toolkit";

    public async ValueTask<Result<Toolkit?>> Find(string name, CancellationToken cancellationToken)
    {
        var rows = await Read(new SqlStatement(Select + " WHERE name = $name", c => c.Bind("$name", name)), cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success<Toolkit?>(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    public ValueTask<Result<IReadOnlyList<Toolkit>>> List(CancellationToken cancellationToken) =>
        Read(new SqlStatement(Select + " ORDER BY name"), cancellationToken);

    public async ValueTask<Result<Unit>> Save(Toolkit toolkit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return LedgerDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var statement = new SqlStatement(
                "INSERT INTO toolkit (name, dir, state, source_hash, built_at, manifest_json, created_by) VALUES ($name, $dir, $state, $hash, $built, $manifest, $by) " +
                "ON CONFLICT(name) DO UPDATE SET dir = $dir, state = $state, source_hash = $hash, built_at = $built, manifest_json = $manifest, created_by = $by",
                c => c.Bind("$name", toolkit.Name).Bind("$dir", toolkit.Dir).Bind("$state", StateName(toolkit.State)).Bind("$hash", toolkit.SourceHash)
                    .Bind("$built", toolkit.BuiltAt?.ToString("O", CultureInfo.InvariantCulture)).Bind("$manifest", toolkit.Manifest?.ToJson()).Bind("$by", toolkit.CreatedBy),
                $"toolkit {toolkit.Name}");
            return await LeaseReads.WriteAndCommit(begun.Value, statement, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static string StateName(ToolkitState state) => state.ToString().ToLowerInvariant();

    private async ValueTask<Result<IReadOnlyList<Toolkit>>> Read(SqlStatement statement, CancellationToken cancellationToken)
    {
        var schema = await database.Schema(cancellationToken).ConfigureAwait(false);
        if (schema.IsError || schema.Value.Applied < LedgerDatabase.ToolkitsVersion)
        {
            return schema.IsError ? schema.Error : Results.Success<IReadOnlyList<Toolkit>>([]);
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return LedgerDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, statement, ToolkitRowMapper.Instance, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? LedgerDatabase.Translate(rows.Error) : rows;
        }
    }

    private sealed class ToolkitRowMapper : IRowMapper<Toolkit>
    {
        internal static readonly ToolkitRowMapper Instance = new();

        public Toolkit Map(SqliteDataReader reader)
        {
            var manifest = reader.IsDBNull(5) ? null : ToolkitManifest.Parse(reader.GetString(5)) is { IsError: false } parsed ? parsed.Value : null;
            return new Toolkit(
                reader.GetString(0),
                reader.GetString(1),
                Enum.TryParse<ToolkitState>(reader.GetString(2), ignoreCase: true, out var state) ? state : ToolkitState.Off,
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                manifest,
                reader.GetString(6));
        }
    }
}
