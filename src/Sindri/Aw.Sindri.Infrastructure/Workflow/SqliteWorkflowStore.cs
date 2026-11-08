using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>'workflow' and 'workflow_revision' in ledger.db (issue 2258, Data): the source of truth for workflow graphs, so they sit beside the ledger and not in views.db. A read never creates or migrates the file.</summary>
/// <param name="database">The ledger file.</param>
/// <param name="clock">The clock the revisions' times come from.</param>
internal sealed class SqliteWorkflowStore(LedgerDatabase database, TimeProvider clock) : IWorkflowStore
{
    public ValueTask<Result<WorkflowRevision>> Create(WorkflowId id, WorkflowSlug slug, GraphSpec spec, string createdBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return RecipeSessions.Write(database, async ValueTask<Result<WorkflowRevision>> (session, token) =>
        {
            var taken = await RecipeSessions.Exists(session, new SqlStatement(WorkflowSql.SlugTaken, c => c.Bind("$slug", slug.Value)), token).ConfigureAwait(false);
            if (taken.IsError || taken.Value)
            {
                return taken.IsError ? taken.Error : SindriErrors.GraphSlugTaken.With($"the slug '{slug.Value}' belongs to another workflow; pick another");
            }

            var inserted = await RecipeSessions.Execute(session, new SqlStatement(WorkflowSql.InsertWorkflow, c => c.Bind("$id", id.Value).Bind("$slug", slug.Value).Bind("$name", spec.Name).Bind("$by", createdBy), $"workflow {slug.Value}"), token).ConfigureAwait(false);
            return inserted.IsError ? inserted.Error : await AddRevision(session, new NewRevision(id, 1, spec, createdBy), token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public ValueTask<Result<WorkflowRevision>> Save(WorkflowId id, int baseRevision, GraphSpec spec, string createdBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return RecipeSessions.Write(database, async ValueTask<Result<WorkflowRevision>> (session, token) =>
        {
            var record = await RecordIn(session, id, token).ConfigureAwait(false);
            if (record.IsError)
            {
                return record.Error;
            }

            if (record.Value.LatestRevision != baseRevision)
            {
                return SindriErrors.GraphStaleRevision.With(string.Create(CultureInfo.InvariantCulture, $"you edited revision {baseRevision}, but the latest is {record.Value.LatestRevision}. Read it again and retry"));
            }

            var renamed = await RecipeSessions.Execute(session, new SqlStatement(WorkflowSql.Rename, c => c.Bind("$id", id.Value).Bind("$name", spec.Name)), token).ConfigureAwait(false);
            return renamed.IsError ? renamed.Error : await AddRevision(session, new NewRevision(id, baseRevision + 1, spec, createdBy), token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public async ValueTask<Result<WorkflowRecord>> Get(WorkflowId id, CancellationToken cancellationToken)
    {
        var found = await RecipeSessions.Read<WorkflowRecord?>(database, LedgerDatabase.WorkflowsVersion, empty: null, (session, token) => FindRecord(session, id, token), cancellationToken).ConfigureAwait(false);
        return Required(found, id);
    }

    public async ValueTask<Result<WorkflowRevision>> GetRevision(WorkflowId id, int? revision, CancellationToken cancellationToken)
    {
        var found = await RecipeSessions.Read<WorkflowRevision?>(database, LedgerDatabase.WorkflowsVersion, empty: null, (session, token) => FindRevision(session, id, revision, token), cancellationToken).ConfigureAwait(false);
        return Required(found, id);
    }

    public ValueTask<Result<IReadOnlyList<WorkflowRecord>>> List(bool includeArchived, CancellationToken cancellationToken) =>
        RecipeSessions.Read<IReadOnlyList<WorkflowRecord>>(database, LedgerDatabase.WorkflowsVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(includeArchived ? WorkflowSql.ListAll : WorkflowSql.ListLive), WorkflowRecordMapper.Instance, token), cancellationToken);

    public ValueTask<Result<WorkflowRecord>> Activate(WorkflowId id, int revision, CancellationToken cancellationToken) =>
        RecipeSessions.Write(database, async ValueTask<Result<WorkflowRecord>> (session, token) =>
        {
            var exists = await FindRevision(session, id, revision, token).ConfigureAwait(false);
            if (exists.IsError || exists.Value is null)
            {
                return exists.IsError ? exists.Error : NotFound(id, string.Create(CultureInfo.InvariantCulture, $"revision {revision}"));
            }

            var pinned = await RecipeSessions.Execute(session, new SqlStatement(WorkflowSql.Pin, c => c.Bind("$id", id.Value).Bind("$revision", revision)), token).ConfigureAwait(false);
            return pinned.IsError ? pinned.Error : await RecordIn(session, id, token).ConfigureAwait(false);
        }, cancellationToken);

    public ValueTask<Result<WorkflowRecord>> Archive(WorkflowId id, CancellationToken cancellationToken) =>
        RecipeSessions.Write(database, async ValueTask<Result<WorkflowRecord>> (session, token) =>
        {
            var record = await RecordIn(session, id, token).ConfigureAwait(false);
            if (record.IsError)
            {
                return record;
            }

            var archived = await RecipeSessions.Execute(session, new SqlStatement(WorkflowSql.Archive, c => c.Bind("$id", id.Value).Bind("$at", clock.GetUtcNow())), token).ConfigureAwait(false);
            return archived.IsError ? archived.Error : await RecordIn(session, id, token).ConfigureAwait(false);
        }, cancellationToken);

    private async ValueTask<Result<WorkflowRevision>> AddRevision(ISqliteSession session, NewRevision next, CancellationToken token)
    {
        var (id, revision, spec, createdBy) = next;
        var at = clock.GetUtcNow();
        var hash = GraphJson.Hash(spec);
        var inserted = await RecipeSessions.Execute(session, new SqlStatement(WorkflowSql.InsertRevision,
            c => c.Bind("$id", id.Value).Bind("$revision", revision).Bind("$spec", GraphJson.Write(spec)).Bind("$hash", hash).Bind("$by", createdBy).Bind("$at", at), string.Create(CultureInfo.InvariantCulture, $"revision {revision}")), token).ConfigureAwait(false);
        return inserted.IsError ? inserted.Error : new WorkflowRevision(id, revision, spec, hash, createdBy, at);
    }

    private static ValueTask<Result<WorkflowRecord?>> FindRecord(ISqliteSession session, WorkflowId id, CancellationToken token) =>
        RecipeSessions.First(session, new SqlStatement(WorkflowSql.ById, c => c.Bind("$id", id.Value)), WorkflowRecordMapper.Instance, token);

    private static ValueTask<Result<WorkflowRevision?>> FindRevision(ISqliteSession session, WorkflowId id, int? revision, CancellationToken token) =>
        RecipeSessions.First(session, new SqlStatement(WorkflowSql.SelectRevision, c => c.Bind("$id", id.Value).Bind("$revision", revision)), WorkflowRevisionMapper.Instance, token);

    private static async ValueTask<Result<WorkflowRecord>> RecordIn(ISqliteSession session, WorkflowId id, CancellationToken token) =>
        Required(await FindRecord(session, id, token).ConfigureAwait(false), id);

    private static Result<T> Required<T>(Result<T?> found, WorkflowId id)
        where T : class =>
        found.IsError ? found.Error : found.Value ?? (Result<T>)NotFound(id, "workflow");

    private static Error NotFound(WorkflowId id, string what) => SindriErrors.GraphNotFound.With($"no {what} for '{id.Value}'; list the workflows to see what exists");

    private sealed record NewRevision(WorkflowId Id, int Number, GraphSpec Spec, string CreatedBy);
}
