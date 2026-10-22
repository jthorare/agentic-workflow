using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Infrastructure.Deps;

namespace Aw.CodeIndex.Tests.Deps;

public sealed class CsprojReaderTests
{
    private readonly CsprojReader _reader = new();

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/App/App.csproj", true)]
    [InlineData("App.CSPROJ", true)]
    [InlineData("App.csproj.user", false)]
    [InlineData("package.json", false)]
    public void Handles_csproj_files(string path, bool expected) => Assert.Equal(expected, _reader.Handles(path));

    [Fact, Trait("Lane", "unit")]
    public void Read_takes_versions_from_attributes_children_and_overrides()
    {
        const string text = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" Version="4.0.0" />
                <PackageReference Include="Vogen"><Version>8.0.1</Version></PackageReference>
                <PackageReference Include="Central" />
                <PackageReference Include="Pinned" VersionOverride="2.0.0" Version="1.0.0" />
                <PackageReference Include="Analyzer" Version="3.0.0" PrivateAssets="all" />
                <ProjectReference Include="../Other/Other.csproj" />
              </ItemGroup>
              <ItemGroup Condition="'$(OS)' == 'Windows_NT'">
                <PackageReference Include="WinOnly" Version="1.1.0" />
              </ItemGroup>
            </Project>
            """;

        var rows = _reader.Read("App.csproj", text);

        Assert.Equal(
            [
                new DepRecord("App.csproj", "Serilog", "4.0.0", "runtime", ""),
                new DepRecord("App.csproj", "Vogen", "8.0.1", "runtime", ""),
                new DepRecord("App.csproj", "Central", "", "runtime", ""),
                new DepRecord("App.csproj", "Pinned", "2.0.0", "runtime", ""),
                new DepRecord("App.csproj", "Analyzer", "3.0.0", "dev", ""),
                new DepRecord("App.csproj", "WinOnly", "1.1.0", "runtime", "conditional"),
            ],
            rows);
    }

    [Fact, Trait("Lane", "unit")]
    public void Read_reads_an_old_style_file_with_a_namespace_and_keeps_the_first_of_a_repeated_name()
    {
        const string text = """
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup><PackageReference Include="Dup" Version="1.0.0" /><PackageReference Include="dup" Version="2.0.0" /></ItemGroup>
            </Project>
            """;

        Assert.Equal([new DepRecord("Old.csproj", "Dup", "1.0.0", "runtime", "")], _reader.Read("Old.csproj", text));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("<Project><ItemGroup>")]
    [InlineData("plain text")]
    [InlineData("<!DOCTYPE Project [<!ENTITY a \"aaaa\">]><Project><ItemGroup><PackageReference Include=\"&a;\" Version=\"1\"/></ItemGroup></Project>")]
    public void Read_gives_no_rows_for_a_malformed_file_or_one_with_a_dtd(string text) => Assert.Empty(_reader.Read("App.csproj", text));
}
