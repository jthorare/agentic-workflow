using System.Xml.Linq;
using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>The registry (Packages.cs) matches the solution, so a new package can't skip the architecture rules.</summary>
[Trait("Lane", "unit")]
public sealed class RegistryTests
{
    /// <summary>A production project in the solution that Packages.cs doesn't list.</summary>
    public const string MissingId = "ARCH-REG-001";

    /// <summary>A Packages.cs line with no production project in the solution, or no ProjectReference here.</summary>
    public const string StaleId = "ARCH-REG-002";

    [Fact]
    public void Every_production_project_in_the_solution_is_registered() =>
        Verify.None(Missing(Solution.ProductionProjects, Packages.Production));

    [Fact]
    public void Every_registered_assembly_is_a_production_project_that_loads() =>
        Verify.None(Stale(Solution.ProductionProjects, Packages.Production)
            .Concat(Packages.Production.Where(n => Solution.ProductionProjects.Contains(n, StringComparer.Ordinal) && !Production.Loaded(n))
                .Select(n => new Violation(StaleId, $"{n} is in Packages.cs but its assembly {Solution.AssemblyOf(n)} doesn't load from the test output", $"add {AddReference(n)}"))));

    [Fact]
    public void Every_registered_assembly_is_in_the_architecture_model()
    {
        var loaded = Production.Architecture.Assemblies.Select(a => a.Name.Split(',')[0]).ToHashSet(StringComparer.Ordinal);

        Assert.All(Packages.Production, n => Assert.Contains(Solution.AssemblyOf(n), loaded));
        Assert.NotEmpty(Production.Edges);
    }

    [Fact]
    public void A_project_missing_from_the_registry_is_reported_with_the_line_to_add() =>
        Verify.Exactly(Missing(["Aw.Kernel", "Aw.Demo.Core"], ["Aw.Kernel"]), MissingId, "Aw.Demo.Core");

    [Fact]
    public void A_registered_name_without_a_project_is_reported() =>
        Verify.Exactly(Stale(["Aw.Kernel"], ["Aw.Kernel", "Aw.Gone.Core"]), StaleId, "Aw.Gone.Core");

    [Fact]
    public void A_project_that_renames_its_assembly_is_loaded_by_the_new_name() =>
        Assert.Equal("judge", Solution.AssemblyNameIn(XDocument.Parse(
            "<Project><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>judge</AssemblyName></PropertyGroup></Project>"),
            "Aw.Judge.Host"));

    [Fact]
    public void A_project_without_an_assembly_name_builds_its_project_name() =>
        Assert.Equal("Aw.Judge.Core", Solution.AssemblyNameIn(XDocument.Parse("<Project><PropertyGroup /></Project>"), "Aw.Judge.Core"));

    [Fact]
    public void Only_src_projects_that_are_not_analyzers_are_production_projects() =>
        Assert.Equal(["Aw.Kernel", "Aw.Judge.Host"], Solution.ProductionOf([
            "src/Aw.Kernel/Aw.Kernel.csproj", "src/Aw.Analyzers/Aw.Analyzers.csproj", "tests/Aw.Kernel.Tests/Aw.Kernel.Tests.csproj",
            "src/Judge/Aw.Judge.Host/Aw.Judge.Host.csproj"]));

    [Fact]
    public void The_tier_zero_list_matches_deps_yaml()
    {
        var blocks = File.ReadAllText(Path.Combine(Solution.RepoRoot, "deps.yaml")).Split("\n  - id: ");
        var declared = blocks
            .Where(b => b.Contains("\n    tier: 0\n", StringComparison.Ordinal))
            .Select(b => b.Split('\n').Select(l => l.Trim()).First(l => l.StartsWith("projectPrefix: ", StringComparison.Ordinal))["projectPrefix: ".Length..])
            .ToList();

        Assert.NotEmpty(declared);
        Assert.All(declared, p => Assert.True(TierZero.Contains(p), $"{p} is tier 0 in deps.yaml; add its prefix to TierZero.Prefixes"));
        Assert.All(TierZero.Prefixes, p => Assert.Contains(p, declared, StringComparer.Ordinal));
    }

    [Fact]
    public void The_compose_edges_come_from_deps_yaml()
    {
        // The reader is line based (one "- " item per edge); every compose edge line must come back as an edge.
        var declared = File.ReadAllText(Path.Combine(Solution.RepoRoot, "deps.yaml")).Split('\n')
            .Count(l => l.TrimEnd('\r') == "    kind: compose");

        Assert.Contains(new ComposeEdge("Aw.Sindri", "Aw.Context"), Production.Compose);
        Assert.Equal(declared, Production.Compose.Count);
    }

    private static IEnumerable<Violation> Missing(IEnumerable<string> projects, IReadOnlyList<string> registered) =>
        projects.Where(p => !registered.Contains(p, StringComparer.Ordinal))
            .Select(p => new Violation(MissingId, $"{p} is in AgenticWorkflow.slnx but not in Packages.cs",
                $"add \"{p}\", to Packages.Production and {AddReference(p)} (/new-package does both)"));

    private static IEnumerable<Violation> Stale(IReadOnlyList<string> projects, IEnumerable<string> registered) =>
        registered.Where(r => !projects.Contains(r, StringComparer.Ordinal))
            .Select(r => new Violation(StaleId, $"{r} is in Packages.cs but no src/ project in AgenticWorkflow.slnx has that name",
                "remove the line, or add the project to the solution"));

    private static string AddReference(string project) =>
        $"a ProjectReference to its .csproj in tests/Aw.Architecture.Tests/Aw.Architecture.Tests.csproj ({project})";
}
