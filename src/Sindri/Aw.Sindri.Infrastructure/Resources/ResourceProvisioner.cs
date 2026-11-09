using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>Plans, creates and checks an owner's resources, and books them in the 'resource' table (bookkeeping only: the data is the files).</summary>
/// <param name="store">Creates the files.</param>
/// <param name="database">The ledger the bookkeeping rows go in.</param>
/// <param name="clock">The clock for the rows' times.</param>
internal sealed class ResourceProvisioner(ResourceStore store, LedgerDatabase database, TimeProvider clock) : IResourceProvisioner
{
    private const string Upsert =
        "INSERT INTO resource (owner, kind, name, path, size_limit, created_at, last_used_at) VALUES ($owner, $kind, $name, $path, $limit, $at, $at) " +
        "ON CONFLICT (owner, kind, name) DO UPDATE SET path = excluded.path, size_limit = excluded.size_limit, last_used_at = excluded.last_used_at, archived_at = NULL";

    private const string Select = "SELECT owner, kind, name, path, size_limit FROM resource WHERE owner = $owner ORDER BY kind, name";

    public async ValueTask<Result<ProvisionedResources>> Ensure(ResourceOwner owner, ResourceSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(spec);
        var planned = ResourcePlanner.Plan(owner, spec, store.Root);
        if (planned.IsError)
        {
            return planned.Error;
        }

        var plan = planned.Value;
        var made = store.Provision(owner, plan);
        if (made.IsError)
        {
            return made.Error;
        }

        foreach (var disk in plan.Create.Where(static c => c is { Kind: ResourceKind.Disk, SizeLimit: not null }))
        {
            var used = DiskMeter.Check(disk.Name, disk.Path, disk.SizeLimit ?? long.MaxValue);
            if (used.IsError)
            {
                return used.Error;
            }
        }

        var booked = await Book(owner, plan, cancellationToken).ConfigureAwait(false);
        return booked.IsError ? booked.Error : new ProvisionedResources(plan.Env, plan.Mounts, plan.Limits);
    }

    /// <summary>Reads the rows booked for <paramref name="owner"/>.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The rows, by kind and name.</returns>
    internal ValueTask<Result<IReadOnlyList<ResourceRow>>> Booked(ResourceOwner owner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return RecipeSessions.Read(database, LedgerDatabase.ResourcesVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(Select, c => c.Bind("$owner", owner.ToString())), ResourceRowMapper.Instance, token), cancellationToken);
    }

    private ValueTask<Result<int>> Book(ResourceOwner owner, ResourcePlan plan, CancellationToken cancellationToken)
    {
        var at = clock.GetUtcNow();
        return RecipeSessions.Write(database, async ValueTask<Result<int>> (session, token) =>
        {
            foreach (var item in plan.Create)
            {
                var kind = item.Kind.ToString().ToLowerInvariant();
                var written = await RecipeSessions.Execute(session, new SqlStatement(
                    Upsert,
                    c => c.Bind("$owner", owner.ToString()).Bind("$kind", kind).Bind("$name", item.Name).Bind("$path", item.Path).Bind("$limit", item.SizeLimit).Bind("$at", at),
                    $"resource {kind} {item.Name}"), token).ConfigureAwait(false);
                if (written.IsError)
                {
                    return written;
                }
            }

            return plan.Create.Count;
        }, cancellationToken);
    }
}
