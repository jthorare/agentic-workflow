using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Infrastructure.Deps;

namespace Aw.CodeIndex.Tests.Deps;

public sealed class PackageJsonReaderTests
{
    private readonly PackageJsonReader _reader = new();

    [Theory, Trait("Lane", "unit")]
    [InlineData("package.json", true)]
    [InlineData("web/app/package.json", true)]
    [InlineData("package-lock.json", false)]
    [InlineData("my-package.json", false)]
    [InlineData("src/A.cs", false)]
    public void Handles_only_files_named_package_json(string path, bool expected) => Assert.Equal(expected, _reader.Handles(path));

    [Fact, Trait("Lane", "unit")]
    public void Read_gives_one_row_per_dependency_with_its_kind()
    {
        const string text = """
            { "name": "app", "version": "1.0.0",
              "dependencies": { "zod": "^3.0.0", "local": "workspace:*" },
              "devDependencies": { "vitest": "~2.1.0", "zod": "^9.9.9" },
              "peerDependencies": { "react": ">=18" },
              "optionalDependencies": { "fsevents": "2.3.3" } }
            """;

        var rows = _reader.Read("web/package.json", text);

        Assert.Equal(
            [
                new DepRecord("web/package.json", "zod", "^3.0.0", "runtime", ""),
                new DepRecord("web/package.json", "local", "workspace:*", "runtime", "workspace"),
                new DepRecord("web/package.json", "vitest", "~2.1.0", "dev", ""),
                new DepRecord("web/package.json", "react", ">=18", "peer", ""),
                new DepRecord("web/package.json", "fsevents", "2.3.3", "optional", ""),
            ],
            rows);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("file:../lib", "local")]
    [InlineData("link:../lib", "local")]
    [InlineData("git+https://example.test/x.git", "url")]
    [InlineData("github:owner/name", "url")]
    [InlineData("https://example.test/x.tgz", "url")]
    [InlineData("^1.2.3", "")]
    public void Read_tags_the_versions_that_are_not_from_the_registry(string version, string tags)
        => Assert.Equal(tags, Assert.Single(_reader.Read("package.json", $"{{\"dependencies\":{{\"x\":\"{version}\"}}}}")).Tags);

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"dependencies": ["a"]}""")]
    [InlineData("""{"dependencies": {"a": 1, "b": null, "": "1"}}""")]
    public void Read_gives_no_rows_for_a_malformed_or_oddly_shaped_file(string text) => Assert.Empty(_reader.Read("package.json", text));

    [Fact, Trait("Lane", "unit")]
    public void Read_survives_a_deeply_nested_file() => Assert.Empty(_reader.Read("package.json", new string('[', 5_000)));
}
