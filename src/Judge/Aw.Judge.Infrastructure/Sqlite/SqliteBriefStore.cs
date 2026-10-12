using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>IBriefStore over decisions.sqlite (the briefs table).</summary>
/// <param name="database">The judge database.</param>
internal sealed class SqliteBriefStore(JudgeDatabase database) : IBriefStore
{
    public async ValueTask<Result<Unit>> Save(Brief brief, CancellationToken cancellationToken)
    {
        var saved = await Write(BriefSql.Save(brief), cancellationToken).ConfigureAwait(false);
        return saved.IsError && saved.Error.Code == DataErrors.Constraint.Code
            ? JudgeErrors.BriefExists.With($"brief already saved: {brief.ToolUseId.Value}")
            : saved;
    }

    public ValueTask<Result<Brief?>> ByToolUseId(ToolUseId toolUseId, CancellationToken cancellationToken) =>
        FirstOrNone(BriefSql.Find(toolUseId), cancellationToken);

    public ValueTask<Result<Brief?>> FirstUnmappedByDispatchName(string dispatchName, CancellationToken cancellationToken) =>
        FirstOrNone(BriefSql.FindByName(dispatchName), cancellationToken);

    public ValueTask<Result<Brief?>> FirstUnmappedByDispatch(SessionId sessionId, PromptId promptId, string subagentType, CancellationToken cancellationToken) =>
        FirstOrNone(BriefSql.FindByDispatch(sessionId, promptId, subagentType), cancellationToken);

    public ValueTask<Result<Unit>> SetAgentId(ToolUseId toolUseId, AgentId agentId, CancellationToken cancellationToken) =>
        Write(BriefSql.Map(toolUseId, agentId), cancellationToken);

    private async ValueTask<Result<Brief?>> FirstOrNone(SqlStatement statement, CancellationToken cancellationToken)
    {
        var begun = await database.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(begun.Value, statement, BriefMapper.Instance, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
        }
    }

    // One statement in its own unit of work, committed.
    private async ValueTask<Result<Unit>> Write(SqlStatement statement, CancellationToken cancellationToken)
    {
        var begun = await database.BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var work = begun.Value;
        await using (work.ConfigureAwait(false))
        {
            var changed = await SqliteQueries.Execute(work, statement, cancellationToken).ConfigureAwait(false);
            return changed.IsError ? changed.Error : await work.Commit(cancellationToken).ConfigureAwait(false);
        }
    }
}
