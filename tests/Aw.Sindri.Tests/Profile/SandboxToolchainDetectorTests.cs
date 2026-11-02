using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The toolchain detector (sandbox spec §3.7, SB-11): a file listing in, the 'sandbox.repos.&lt;repo&gt;' entries to offer out. Pure, so these tests give paths and read no disk.</summary>
public sealed class SandboxToolchainDetectorTests
{
    private static ToolchainOffer Detect(params string[] paths) => SandboxToolchainDetector.Detect(paths);

    [Fact, Trait("Lane", "unit")]
    public void A_python_repo_is_offered_the_pypi_hosts_and_the_pip_cache()
    {
        var offer = Detect("README.md", "pyproject.toml", "src/app/main.py");

        var python = Assert.Single(offer.Toolchains);
        Assert.Equal(("Python", "pyproject.toml"), (python.Name, python.Evidence));
        Assert.Equal(["pypi.org", "files.pythonhosted.org"], offer.Settings?.Hosts);
        Assert.Equal([".cache/pip"], offer.Settings?.CachePaths);
        Assert.Null(offer.Settings?.Groups);
        Assert.Null(offer.Settings?.Prepare);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("requirements.txt")]
    [InlineData("requirements-dev.txt")]
    [InlineData("setup.py")]
    [InlineData("services/api/pyproject.toml")]
    public void Each_python_project_file_shows_python(string file) => Assert.Equal("Python", Assert.Single(Detect(file).Toolchains).Name);

    [Fact, Trait("Lane", "unit")]
    public void A_go_repo_is_offered_the_go_module_hosts_and_the_go_caches()
    {
        var offer = Detect("go.mod", "go.sum", "cmd/app/main.go");

        var go = Assert.Single(offer.Toolchains);
        Assert.Equal(("Go", "go.mod"), (go.Name, go.Evidence));
        Assert.Equal(["proxy.golang.org", "sum.golang.org"], offer.Settings?.Hosts);
        Assert.Equal(["go/pkg/mod", ".cache/go-build"], offer.Settings?.CachePaths);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_repo_gets_the_packages_group_and_a_prepare_dir_for_each_lock_file()
    {
        var offer = Detect("package.json", "package-lock.json", "scripts/package.json", "scripts/package-lock.json", "skills/x/package.json");

        Assert.Equal("Node", Assert.Single(offer.Toolchains).Name);
        Assert.Equal(["packages"], offer.Settings?.Groups);
        Assert.Equal([".", "scripts"], offer.Settings?.Prepare?.Dirs?.Select(static d => d.Dir), StringComparer.Ordinal);
        Assert.All(offer.Settings?.Prepare?.Dirs ?? [], static d => Assert.Equal(("npm ci", null), (d.Tool, d.AllowScripts)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Node_without_a_lock_file_gets_no_prepare_and_node_modules_is_never_a_prepare_dir()
    {
        var offer = Detect("package.json", "node_modules/left-pad/package.json", "node_modules/left-pad/package-lock.json");

        Assert.Equal(["packages"], offer.Settings?.Groups);
        Assert.Null(offer.Settings?.Prepare);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/App/App.csproj", "src/App/App.csproj")]
    [InlineData("global.json", "global.json")]
    public void A_dotnet_repo_gets_the_packages_group(string file, string evidence)
    {
        var offer = Detect(file);

        var dotnet = Assert.Single(offer.Toolchains);
        Assert.Equal(("dotnet", evidence), (dotnet.Name, dotnet.Evidence));
        Assert.Equal(["packages"], offer.Settings?.Groups);
        Assert.Null(offer.Settings?.Hosts);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_no_known_project_file_is_offered_nothing()
    {
        var offer = Detect("README.md", "docs/a.md", "Makefile");

        Assert.Empty(offer.Toolchains);
        Assert.Null(offer.Settings);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_of_several_toolchains_adds_their_entries_up_without_repeats()
    {
        var offer = Detect("web/package.json", "web/package-lock.json", "api/App.csproj", "tools/pyproject.toml", "tools/requirements.txt");

        Assert.Equal(["Node", "Python", "dotnet"], offer.Toolchains.Select(static t => t.Name), StringComparer.Ordinal);
        Assert.Equal(["packages"], offer.Settings?.Groups);
        Assert.Equal(["pypi.org", "files.pythonhosted.org"], offer.Settings?.Hosts);
        Assert.Equal(["web"], offer.Settings?.Prepare?.Dirs?.Select(static d => d.Dir), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void What_is_offered_passes_the_profile_checks_for_every_toolchain()
    {
        var offer = Detect("package.json", "package-lock.json", "pyproject.toml", "go.mod", "A.csproj");

        var settings = new SandboxSettings(Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["demo"] = offer.Settings! });
        Assert.Null(SandboxRules.Check(settings));
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_toolchain_says_in_one_line_what_it_is_offered()
    {
        var offer = Detect("pyproject.toml", "go.mod");

        Assert.Equal(
            [
                "Go (go.mod): hosts proxy.golang.org, sum.golang.org; cache paths go/pkg/mod, .cache/go-build",
                "Python (pyproject.toml): hosts pypi.org, files.pythonhosted.org; cache paths .cache/pip",
            ],
            offer.Toolchains.Select(static t => t.Line()).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.DoesNotContain(offer.Toolchains.Select(static t => t.Line()), static l => l.Contains('\n', StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_snippet_is_a_sandbox_section_with_only_this_repos_entry()
    {
        var offer = Detect("package.json", "scripts/package.json", "scripts/package-lock.json", "pyproject.toml");

        var yaml = SandboxOfferYaml.Render("demo", offer.Settings!).ReplaceLineEndings("\n");

        Assert.Equal(
            "sandbox:\n  repos:\n    demo:\n      groups: [packages]\n      hosts: [pypi.org, files.pythonhosted.org]\n      cachePaths: [.cache/pip]\n"
            + "      prepare:\n        dirs:\n          - dir: scripts\n            tool: npm ci\n",
            yaml);
    }
}
