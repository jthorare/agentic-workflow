using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Workflow.Vulnerabilities;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class LockfileReaderTests
{
    public static TheoryData<string, string, string, string, string> Fixtures() => new()
    {
        { "packages.lock.json", """{"version":2,"dependencies":{"net10.0":{"Newtonsoft.Json":{"type":"Direct","requested":"[13.0.3, )","resolved":"13.0.3"},"Aw.Core":{"type":"Project"}}}}""", "NuGet", "Newtonsoft.Json", "13.0.3" },
        { "package-lock.json", """{"lockfileVersion":3,"packages":{"":{"name":"app","version":"1.0.0"},"node_modules/lodash":{"version":"4.17.21"}}}""", "npm", "lodash", "4.17.21" },
        { "go.sum", "github.com/pkg/errors v0.9.1 h1:abc=\ngithub.com/pkg/errors v0.9.1/go.mod h1:def=\n", "Go", "github.com/pkg/errors", "0.9.1" },
        { "Cargo.lock", "version = 3\n\n[[package]]\nname = \"serde\"\nversion = \"1.0.200\"\nsource = \"registry+https://github.com/rust-lang/crates.io-index\"\n", "crates.io", "serde", "1.0.200" },
        { "poetry.lock", "[[package]]\nname = \"requests\"\nversion = \"2.31.0\"\ndescription = \"HTTP\"\n", "PyPI", "requests", "2.31.0" },
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Each_supported_lockfile_yields_ecosystem_name_and_version(string file, string content, string ecosystem, string name, string version)
    {
        var dir = Directory.CreateTempSubdirectory("lockfile-reader-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, file), content);

            var result = LockfileReader.ReadDirectory(dir.FullName);

            Assert.True(result.IsSuccess);
            Assert.Equal([new VulnPackage(ecosystem, name, version)], result.Value);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
