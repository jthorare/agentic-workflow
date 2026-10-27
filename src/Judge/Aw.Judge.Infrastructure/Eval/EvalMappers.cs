using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Infrastructure.Eval;

/// <summary>Maps EvalSql's columns to the eval store's rows.</summary>
internal static class EvalMappers
{
    internal static readonly IRowMapper<EvalItem> Item = new ItemMapper();
    internal static readonly IRowMapper<LabeledItem> Labeled = new LabeledMapper();
    internal static readonly IRowMapper<LabelCount> Count = new CountMapper();
    internal static readonly IRowMapper<LabelAgreement> Agreement = new AgreementMapper();
    internal static readonly IRowMapper<DecidedInput> Decided = new DecidedMapper();
    internal static readonly IRowMapper<OutcomeCandidate> Candidate = new CandidateMapper();

    // Columns 0-5 are EvalSql.ItemColumns.
    private static EvalItem ReadItem(SqliteDataReader reader) => new(
        EvalItemId.From(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4), SqliteRead.GetTimestamp(reader, 5));

    private sealed class ItemMapper : IRowMapper<EvalItem>
    {
        public EvalItem Map(SqliteDataReader reader) => ReadItem(reader);
    }

    private sealed class LabeledMapper : IRowMapper<LabeledItem>
    {
        public LabeledItem Map(SqliteDataReader reader)
        {
            _ = LabelSources.TryParse(reader.GetString(7), out var source);
            return new LabeledItem(ReadItem(reader), reader.GetString(6), source);
        }
    }

    private sealed class CountMapper : IRowMapper<LabelCount>
    {
        public LabelCount Map(SqliteDataReader reader) =>
            new(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
    }

    // SUM over no rows is NULL.
    private sealed class AgreementMapper : IRowMapper<LabelAgreement>
    {
        public LabelAgreement Map(SqliteDataReader reader) =>
            new(reader.GetInt32(0), reader.IsDBNull(1) ? 0 : reader.GetInt32(1));
    }

    private sealed class DecidedMapper : IRowMapper<DecidedInput>
    {
        public DecidedInput Map(SqliteDataReader reader) => new(
            DecisionId.From(reader.GetString(0)), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3));
    }

    private sealed class CandidateMapper : IRowMapper<OutcomeCandidate>
    {
        public OutcomeCandidate Map(SqliteDataReader reader) => new(
            EvalItemId.From(reader.GetString(0)), SqliteRead.GetTimestamp(reader, 1), reader.IsDBNull(2) ? "" : reader.GetString(2),
            reader.IsDBNull(3) ? null : SessionId.From(reader.GetString(3)));
    }
}
