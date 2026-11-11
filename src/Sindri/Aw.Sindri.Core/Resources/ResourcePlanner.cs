using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sandbox.Resources;

namespace Aw.Sindri.Core.Resources;

/// <summary>Turns an owner's declarations into what to create, the environment, the mounts and the limit overrides. Pure.</summary>
public static partial class ResourcePlanner
{
    /// <summary>Plans an owner's resources.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="spec">What it declares.</param>
    /// <param name="resourcesRoot">The resources root, <c lang="text">$AW_SINDRI_HOME/resources</c>.</param>
    /// <returns>The plan, or SND-RES-002 for a name used twice, SND-RES-006 for a bad name or size.</returns>
    public static Result<ResourcePlan> Plan(ResourceOwner owner, ResourceSpec spec, string resourcesRoot)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(resourcesRoot);
        var home = $"{resourcesRoot.TrimEnd('/')}/{owner.Folder}";
        var create = new List<PlannedResource>();
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        var bad = AddDatabases(spec, home, create, env) ?? AddDisks(spec, home, create, env) ?? AddConnections(spec, env);
        if (bad is not null)
        {
            return bad;
        }

        // why: SQLite writes <name>.db-journal, -wal and -shm next to the file, so a lone file mount can't take a write; the owner's own folder holds only its resources
        List<ResourceMount> mounts = create.Count == 0 ? [] : [new ResourceMount("resources", home)];
        var limits = Limits(spec.Sandbox);
        return limits.IsError ? limits.Error : new ResourcePlan(create, env, mounts, limits.Value);
    }

    private static Error? AddDatabases(ResourceSpec spec, string home, List<PlannedResource> create, Dictionary<string, string> env)
    {
        foreach (var name in spec.Databases.Select(static d => d.Name))
        {
            if (Check(name, "database", create, ResourceKind.Database) is { } bad)
            {
                return bad;
            }

            var path = $"{home}/{name}.db";
            create.Add(new PlannedResource(ResourceKind.Database, name, path, SizeLimit: null));
            env[ResourceEnv.Database(name)] = path;
        }

        return null;
    }

    private static Error? AddDisks(ResourceSpec spec, string home, List<PlannedResource> create, Dictionary<string, string> env)
    {
        foreach (var disk in spec.Disks)
        {
            if (Check(disk.Name, "disk", create, ResourceKind.Disk) is { } bad)
            {
                return bad;
            }

            if (!ResourceSizes.TryParse(disk.Size, out var limit))
            {
                return SindriErrors.ResourceInvalid.With($"The size of disk '{disk.Name}' is a whole number and KiB, MiB, GiB or TiB, such as 512MiB.");
            }

            var path = $"{home}/{disk.Name}";
            create.Add(new PlannedResource(ResourceKind.Disk, disk.Name, path, limit));
            env[ResourceEnv.Disk(disk.Name)] = path;
        }

        return null;
    }

    private static Error? AddConnections(ResourceSpec spec, Dictionary<string, string> env)
    {
        var connections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var connection in spec.Connections)
        {
            if (connection.Name is not { } name || !NameShape.IsMatch(name))
            {
                return NameInvalid("connection");
            }

            if (!connections.Add(name))
            {
                return SindriErrors.ResourceNameClash.With($"Two connections are named '{name}'.");
            }

            env[ResourceEnv.Connection(name)] = name;
        }

        return null;
    }

    private static Error? Check(string? name, string what, List<PlannedResource> made, ResourceKind kind)
    {
        if (!NameShape.IsMatch(name ?? string.Empty))
        {
            return NameInvalid(what);
        }

        return made.Exists(m => m.Kind == kind && string.Equals(m.Name, name, StringComparison.Ordinal))
            ? SindriErrors.ResourceNameClash.With($"Two {what}s are named '{name}'.")
            : null;
    }

    private static Error NameInvalid(string what) => SindriErrors.ResourceInvalid.With($"A {what} name is lowercase letters, digits and '-', 1 to 64 characters, starting with a letter or digit.");

    // Declared limits pass through as text: LimitsResolver clamps them to the profile and the box. They must be exactly what
    // SandboxUnits.ParseSize (KiB, MiB, GiB, TiB only) and ParsePercent accept, or the resolver would throw on a launch:
    // the short forms the graph validator accepts (4g) are converted to that KiB..TiB spelling here.
    private static Result<ResourceLimitOverride?> Limits(SandboxResource? sandbox)
    {
        if (sandbox is null)
        {
            return (ResourceLimitOverride?)null;
        }

        string? memory = null;
        if (sandbox.Memory is not null && !ResourceSizes.TryNormalize(sandbox.Memory, out memory))
        {
            return SindriErrors.ResourceInvalid.With("A sandbox memory is a whole number and k, m, g, t, KiB, MiB, GiB or TiB, such as 4g or 4GiB.");
        }

        if (sandbox.Cpu is not null && !CpuShape.IsMatch(sandbox.Cpu))
        {
            return SindriErrors.ResourceInvalid.With("A sandbox cpu is a percent such as 50%.");
        }

        TimeSpan? wall = null;
        if (sandbox.Wall is not null)
        {
            if (!RecipeDuration.TryParse(sandbox.Wall, provider: null, out var duration))
            {
                return SindriErrors.ResourceInvalid.With("A sandbox wall is a whole number and m, h or d, such as 45m.");
            }

            wall = duration.Span;
        }

        return new ResourceLimitOverride(memory, sandbox.Cpu, wall);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NameShape { get; }

    [GeneratedRegex("^[1-9][0-9]{0,2}%\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CpuShape { get; }
}
