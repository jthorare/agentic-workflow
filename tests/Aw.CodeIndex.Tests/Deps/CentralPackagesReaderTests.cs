using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Infrastructure.Deps;

namespace Aw.CodeIndex.Tests.Deps;

public sealed class CentralPackagesReaderTests
{
    private readonly CentralPackagesReader _reader = new();

    [Theory, Trait("Lane", "unit")]
    [InlineData("Directory.Packages.props", true)]
    [InlineData("src/Directory.Packages.props", true)]
    [InlineData("Directory.Build.props", false)]
    [InlineData("App.csproj", false)]
    public void Handles_only_directory_packages_props(string path, bool expected) => Assert.Equal(expected, _reader.Handles(path));

    [Fact, Trait("Lane", "unit")]
    public void Read_gives_package_versions_and_global_references()
    {
        const string text = """
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="xunit.v3" Version="1.2.3" />
                <PackageVersion Include="Roslyn" Version="5.9.0" Condition="'$(Slow)' != ''" />
                <GlobalPackageReference Include="Meziantou.Analyzer" Version="2.0.1" />
              </ItemGroup>
            </Project>
            """;

        Assert.Equal(
            [
                new DepRecord("Directory.Packages.props", "xunit.v3", "1.2.3", "central", ""),
                new DepRecord("Directory.Packages.props", "Roslyn", "5.9.0", "central", "conditional"),
                new DepRecord("Directory.Packages.props", "Meziantou.Analyzer", "2.0.1", "global", ""),
            ],
            _reader.Read("Directory.Packages.props", text));
    }

    [Fact, Trait("Lane", "unit")]
    public void Read_stops_at_the_per_manifest_limit()
    {
        var items = string.Concat(Enumerable.Range(0, CodeIndexRules.MaxDepsPerManifest + 10).Select(i => $"<PackageVersion Include=\"P{i.ToString(CultureInfo.InvariantCulture)}\" Version=\"1\"/>"));

        Assert.Equal(CodeIndexRules.MaxDepsPerManifest, _reader.Read("Directory.Packages.props", $"<Project><ItemGroup>{items}</ItemGroup></Project>").Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Read_gives_no_rows_for_a_malformed_file() => Assert.Empty(_reader.Read("Directory.Packages.props", "<Project>"));
}
