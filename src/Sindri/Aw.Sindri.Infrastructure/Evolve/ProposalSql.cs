using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Evolve;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Evolve;

/// <summary>The statements and the row mapping of the proposal store.</summary>
internal static class ProposalSql
{
    internal const string Select =
        "SELECT p.id, p.state, p.tier, p.attempts, p.held_by, p.body, p.origin, p.created_at, p.updated_at, " +
        "(SELECT json_group_array(s.session) FROM proposal_source s WHERE s.proposal_id = p.id), p.draft_branch, p.draft_commit FROM proposal p";

    internal static readonly System.Text.Json.JsonSerializerOptions Json = AwJson.CreateOptions(EvolveJsonContext.Default);

    internal static string Name<TEnum>(TEnum value)
        where TEnum : struct, Enum => value.ToString().ToLowerInvariant();

    internal static TEnum Parse<TEnum>(string text, TEnum fallback)
        where TEnum : struct, Enum => Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) ? value : fallback;

    internal static string Stamp(DateTimeOffset at) => at.ToString("O", CultureInfo.InvariantCulture);

    internal static string Write<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, Json));

    internal static Result<T> Read<T>(string json) => AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(json), Json);

    // why: a row whose body no longer parses (written by another build, edited by hand) reads as an error for that row, never as an exception
    internal static Result<StoredProposal> ToStored(ProposalRow row)
    {
        var body = Read<Proposal>(row.Body);
        if (body.IsError)
        {
            return body.Error;
        }

        var sessions = row.Sessions is null ? Results.Success(new List<string>()) : Read<List<string>>(row.Sessions);
        return new StoredProposal(
            ProposalId.From(row.Id),
            body.Value,
            Parse(row.State, ProposalState.Held),
            Parse(row.Tier, ProposalTier.Approval),
            row.Attempts,
            row.HeldBy is null ? null : Parse(row.HeldBy, LeakLayer.Rewrite),
            sessions.IsError ? [] : [.. sessions.Value.Order(StringComparer.Ordinal)],
            row.Origin,
            DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(row.UpdatedAt, CultureInfo.InvariantCulture),
            row.DraftBranch is { } branch && row.DraftCommit is { } commit ? new ProposalDraft(branch, commit) : null);
    }

    internal sealed class RowMapper : IRowMapper<ProposalRow>
    {
        internal static readonly RowMapper Instance = new();

        public ProposalRow Map(SqliteDataReader reader) => new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11));
    }

    internal sealed class TurnsMapper : IRowMapper<string>
    {
        internal static readonly TurnsMapper Instance = new();

        public string Map(SqliteDataReader reader) => reader.GetString(0);
    }
}
