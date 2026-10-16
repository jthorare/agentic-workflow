using Aw.Kernel.Data;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Maps a 'workflow_claims' row (<see cref="ClaimSql.SelectClaim"/> column order).</summary>
internal sealed class WorkflowClaimMapper : IRowMapper<WorkflowClaim>
{
    public static readonly WorkflowClaimMapper Instance = new();

    public WorkflowClaim Map(SqliteDataReader reader) =>
        new(ItemKey.From(reader.GetString(0)), InstanceAddress.From(reader.GetString(1)), reader.GetInt32(2));
}
