using System.Reflection;
using ArchUnitNET.Loader;
using Aw.Architecture.Tests.Rules;
using ArchArchitecture = ArchUnitNET.Domain.Architecture;

namespace Aw.Architecture.Tests;

/// <summary>The registered production assemblies (Packages.cs), loaded once for every rule.</summary>
internal static class Production
{
    private static readonly Lazy<ArchArchitecture> _loaded = new(Load);

    /// <summary>The registered projects' assemblies, loaded by assembly name from the test's output directory.</summary>
    public static IReadOnlyList<Assembly> Assemblies { get; } =
        [.. Packages.Production.Select(Solution.AssemblyOf).Select(TryLoad).OfType<Assembly>()];

    private static readonly string[] _layerProjects = ["Core", "Features", "Infrastructure", "Host", "App"];

    private static ArchArchitecture Load()
    {
        var loader = new ArchLoader().LoadAssemblies([.. Assemblies]);
        return loader.Build();
    }

    private static Assembly? TryLoad(string name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch (FileNotFoundException)
        {
            return null; // RegistryTests reports it (ARCH-REG-002).
        }
    }

    /// <summary>The ArchUnitNET model of the registered assemblies.</summary>
    public static ArchArchitecture Architecture => _loaded.Value;

    /// <summary>
    /// The shaped packages: roots (Aw.Judge for Aw.Judge.Core, Aw.Judge.Features, ... and Aw.Judge.App) and their Host
    /// or App assemblies, by the name each builds (a Host may rename its binary). Tier-0 packages (Aw.Kernel,
    /// Aw.Kernel.Data) have no layers, so only the cycle, naming, service-provider and typed-id rules cover them.
    /// </summary>
    public static Scope Scope { get; } = ScopeOf(Packages.Production.Select(p => (p, Solution.AssemblyOf(p))));

    /// <summary>The scope of registered projects: roots from the project names, Hosts and Apps by the assembly each builds.</summary>
    /// <param name="projects">(project name, assembly name) pairs.</param>
    /// <returns>The scope.</returns>
    public static Scope ScopeOf(IEnumerable<(string Project, string Assembly)> projects)
    {
        List<(string Project, string Assembly)> list = [.. projects];
        var roots = RootsOf(list.Select(p => p.Project));
        var scope = Scope.Of(roots, list
            .Where(p => p.Project.EndsWith(".Host", StringComparison.Ordinal) || p.Project.EndsWith(".App", StringComparison.Ordinal))
            .Select(p => KeyValuePair.Create(p.Assembly, p.Project[..p.Project.LastIndexOf('.')]))
            .Where(p => roots.Contains(p.Value, StringComparer.Ordinal)));
        return scope with { Unshaped = [.. Packages.Unshaped.Where(u => list.Exists(p => p.Project == u))] };
    }

    /// <summary>
    /// The projects that are neither tier 0, a layer project of a shaped package (Core, Features, Infrastructure, Host,
    /// App), nor declared unshaped: each is a misnamed project the layer rules would miss (ARCH-REG-003). A project
    /// whose root namespace sits in its package's layer (Aw.Sindri.Workflows, root namespace
    /// Aw.Sindri.Infrastructure.Workflows) is a layer project: the layer rules read namespaces, so they cover it.
    /// </summary>
    /// <param name="projectNames">Project names.</param>
    /// <param name="declaredUnshaped">The declared unshaped projects (Packages.Unshaped).</param>
    /// <param name="rootNamespaceOf">A project's root namespace; the project name when omitted.</param>
    /// <returns>The undeclared ones, sorted.</returns>
    public static IReadOnlyList<string> UndeclaredUnshaped(
        IEnumerable<string> projectNames, IEnumerable<string> declaredUnshaped, Func<string, string>? rootNamespaceOf = null) =>
        [.. projectNames
            .Where(n => !TierZero.Contains(n) && !_layerProjects.Contains(n.Split('.')[^1], StringComparer.Ordinal)
                && !InLayerNamespace(n, rootNamespaceOf?.Invoke(n) ?? n))
            .Except(declaredUnshaped, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static bool InLayerNamespace(string project, string rootNamespace)
    {
        var package = project[..project.LastIndexOf('.')];
        return _layerProjects.Any(l => rootNamespace == $"{package}.{l}" || rootNamespace.StartsWith($"{package}.{l}.", StringComparison.Ordinal));
    }

    /// <summary>Whether a registered project's assembly loaded.</summary>
    /// <param name="project">A Packages.cs line.</param>
    /// <returns>True when it is in <see cref="Assemblies"/>.</returns>
    public static bool Loaded(string project) => Assemblies.Any(a => a.GetName().Name == Solution.AssemblyOf(project));

    /// <summary>The package roots a list of assembly names implies.</summary>
    /// <param name="assemblyNames">Assembly names.</param>
    /// <returns>Distinct roots, sorted.</returns>
    public static IReadOnlyList<string> RootsOf(IEnumerable<string> assemblyNames) =>
        [.. assemblyNames
            .Where(n => !TierZero.Contains(n))
            .Select(n => n.Split('.'))
            .Where(s => s.Length > 2 && _layerProjects.Contains(s[^1], StringComparer.Ordinal))
            .Select(s => string.Join('.', s[..^1]))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    /// <summary>The compose edges deps.yaml declares, by package root (ARCH-COMPOSE-001, the ARCH-DIR-004 carve-out).</summary>
    public static IReadOnlyList<ComposeEdge> Compose { get; } =
        ComposeEdge.Parse(File.ReadAllText(Path.Combine(Solution.RepoRoot, "deps.yaml")));

    private static readonly Lazy<IReadOnlyList<Edge>> _edges = new(() => Edge.All(Architecture.Types, Scope));

    /// <summary>Every dependency edge among the registered assemblies' types, built once.</summary>
    public static IReadOnlyList<Edge> Edges => _edges.Value;
}
