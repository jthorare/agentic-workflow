using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Workflow.Vulnerabilities;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class VulnerabilitiesBlockTests
{
    private const string BatchAnswer = """{"results":[{"vulns":[{"id":"GHSA-35jh-r3h4-6jhm","modified":"2025-01-01T00:00:00Z"}]},{}]}""";

    private const string Advisory = """{"id":"GHSA-35jh-r3h4-6jhm","aliases":["CVE-2021-23337"],"summary":"Command injection in lodash","database_specific":{"severity":"HIGH"},"affected":[{"package":{"ecosystem":"npm","name":"lodash"},"ranges":[{"type":"SEMVER","events":[{"introduced":"0"},{"fixed":"4.17.21"}]}]}]}""";

    private static readonly IReadOnlySet<string> _hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "api.osv.dev" };

    private static StubHttp Osv() => new((request, _) => request.RequestUri!.AbsolutePath switch
    {
        "/v1/querybatch" => StubHttp.Json(BatchAnswer),
        "/v1/vulns/GHSA-35jh-r3h4-6jhm" => StubHttp.Json(Advisory),
        _ => StubHttp.Json("{}", System.Net.HttpStatusCode.NotFound),
    });

    [Fact]
    public async Task A_package_list_maps_OSV_results_to_vulns_with_fixedIn()
    {
        var stub = Osv();
        var block = new VulnerabilitiesBlock([new OsvProvider(stub.Client(), _hosts)], new NoLockfiles());
        var config = new JsonObject
        {
            ["packages"] = new JsonArray(
                new JsonObject { ["ecosystem"] = "npm", ["name"] = "lodash", ["version"] = "4.17.20" },
                new JsonObject { ["ecosystem"] = "npm", ["name"] = "left-pad", ["version"] = "1.3.0" }),
        };

        var result = await block.Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Outputs["count"]!.GetValue<int>());
        var vuln = result.Value.Outputs["vulns"]!.AsArray().Single()!.AsObject();
        Assert.Equal("GHSA-35jh-r3h4-6jhm", vuln["id"]!.GetValue<string>());
        Assert.Equal("CVE-2021-23337", string.Join(',', vuln["aliases"]!.AsArray().Select(a => a!.GetValue<string>())));
        Assert.Equal("Command injection in lodash", vuln["summary"]!.GetValue<string>());
        Assert.Equal("HIGH", vuln["severity"]!.GetValue<string>());
        Assert.Equal("lodash", vuln["package"]!.GetValue<string>());
        Assert.Equal("4.17.20", vuln["version"]!.GetValue<string>());
        Assert.Equal("4.17.21", vuln["fixedIn"]!.GetValue<string>());
        Assert.Equal("https://osv.dev/vulnerability/GHSA-35jh-r3h4-6jhm", vuln["url"]!.GetValue<string>());
        Assert.All(stub.Calls, call => Assert.Equal("api.osv.dev", call.Uri.Host));
    }

    [Fact]
    public async Task A_repo_reads_its_lockfiles_through_the_lockfile_source()
    {
        var stub = Osv();
        var source = new ScriptedLockfiles([new VulnPackage("npm", "lodash", "4.17.20")]);
        var block = new VulnerabilitiesBlock([new OsvProvider(stub.Client(), _hosts)], source);

        var result = await block.Execute(BlockFakes.Context(new JsonObject { ["repo"] = "acme/web" }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["acme/web"], source.Asked);
        Assert.Equal(1, result.Value.Outputs["count"]!.GetValue<int>());
    }

    private sealed class NoLockfiles : ILockfileSource
    {
        public ValueTask<Result<IReadOnlyList<VulnPackage>>> Read(string repo, CancellationToken cancellationToken) => throw new InvalidOperationException("no repo was configured");
    }

    private sealed class ScriptedLockfiles(IReadOnlyList<VulnPackage> packages) : ILockfileSource
    {
        public List<string> Asked { get; } = [];

        public ValueTask<Result<IReadOnlyList<VulnPackage>>> Read(string repo, CancellationToken cancellationToken)
        {
            Asked.Add(repo);
            return ValueTask.FromResult(Results.Success(packages));
        }
    }
}
