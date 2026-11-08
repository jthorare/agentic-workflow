using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IWorkflowStore"/>. Passes <see cref="WorkflowStoreContract"/>.</summary>
/// <param name="clock">The clock the revisions' times come from.</param>
public sealed class InMemoryWorkflowStore(TimeProvider clock) : IWorkflowStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<WorkflowId, Entry> _workflows = [];

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRevision>> Create(WorkflowId id, WorkflowSlug slug, GraphSpec spec, string createdBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        lock (_gate)
        {
            if (_workflows.Values.Any(e => e.Slug == slug))
            {
                return Done<WorkflowRevision>(SindriErrors.GraphSlugTaken.With($"the slug '{slug.Value}' belongs to another workflow; pick another"));
            }

            var entry = new Entry(slug, spec.Name, createdBy);
            _workflows[id] = entry;
            return Ok(entry.Add(id, spec, createdBy, clock.GetUtcNow()));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRevision>> Save(WorkflowId id, int baseRevision, GraphSpec spec, string createdBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        lock (_gate)
        {
            if (!_workflows.TryGetValue(id, out var entry))
            {
                return Done<WorkflowRevision>(NotFound(id));
            }

            if (entry.Revisions.Count != baseRevision)
            {
                return Done<WorkflowRevision>(SindriErrors.GraphStaleRevision.With(string.Create(CultureInfo.InvariantCulture, $"you edited revision {baseRevision}, but the latest is {entry.Revisions.Count}. Read it again and retry")));
            }

            entry.Name = spec.Name;
            return Ok(entry.Add(id, spec, createdBy, clock.GetUtcNow()));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRecord>> Get(WorkflowId id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return _workflows.TryGetValue(id, out var entry) ? Ok(entry.Record(id)) : Done<WorkflowRecord>(NotFound(id));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRevision>> GetRevision(WorkflowId id, int? revision, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_workflows.TryGetValue(id, out var entry))
            {
                return Done<WorkflowRevision>(NotFound(id));
            }

            var number = revision ?? entry.Revisions.Count;
            return number >= 1 && number <= entry.Revisions.Count ? Ok(entry.Revisions[number - 1]) : Done<WorkflowRevision>(NotFound(id));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowRecord>>> List(bool includeArchived, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Ok<IReadOnlyList<WorkflowRecord>>([.. _workflows.Where(p => includeArchived || p.Value.ArchivedAt is null).OrderBy(p => p.Value.Slug.Value, StringComparer.Ordinal).Select(p => p.Value.Record(p.Key))]);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRecord>> Activate(WorkflowId id, int revision, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_workflows.TryGetValue(id, out var entry) || revision < 1 || revision > entry.Revisions.Count)
            {
                return Done<WorkflowRecord>(NotFound(id));
            }

            entry.Active = revision;
            return Ok(entry.Record(id));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRecord>> Archive(WorkflowId id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_workflows.TryGetValue(id, out var entry))
            {
                return Done<WorkflowRecord>(NotFound(id));
            }

            entry.ArchivedAt = clock.GetUtcNow();
            entry.Active = null;
            return Ok(entry.Record(id));
        }
    }

    private static Error NotFound(WorkflowId id) => SindriErrors.GraphNotFound.With($"no workflow or revision for '{id.Value}'; list the workflows to see what exists");

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private sealed class Entry(WorkflowSlug slug, string name, string createdBy)
    {
        public WorkflowSlug Slug { get; } = slug;

        public string Name { get; set; } = name;

        public int? Active { get; set; }

        public DateTimeOffset? ArchivedAt { get; set; }

        public List<WorkflowRevision> Revisions { get; } = [];

        public WorkflowRevision Add(WorkflowId id, GraphSpec spec, string by, DateTimeOffset at)
        {
            var revision = new WorkflowRevision(id, Revisions.Count + 1, spec, GraphJson.Hash(spec), by, at);
            Revisions.Add(revision);
            return revision;
        }

        public WorkflowRecord Record(WorkflowId id) => new(id, Slug, Name, Active, Revisions.Count, ArchivedAt, createdBy);
    }
}
