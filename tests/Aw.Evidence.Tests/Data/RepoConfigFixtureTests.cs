using Aw.Evidence.Core.Data;
using Aw.Evidence.Tests.Schemas;

namespace Aw.Evidence.Tests.Data;

/// <summary>The fake acme repo's evidence.json (tests/fixtures/repo-config/, checked against schemas/repo-config/evidence.schema.json) is a file TargetsReader accepts.</summary>
public sealed class RepoConfigFixtureTests
{
    private static string AcmeEvidence() =>
        Path.Combine(EvidenceSchemaTests.RepoRoot(), "tests", "fixtures", "repo-config", "acme", ".agentic-workflow", "evidence.json");

    [Fact, Trait("Lane", "unit")]
    public void The_acme_evidence_json_loads_through_the_targets_reader()
    {
        var read = TargetsReader.Read(File.ReadAllBytes(AcmeEvidence()), TestJson.Options);

        var targets = read.Value;
        Assert.Equal(["out/bin"], targets.BuildOutputs);
        Assert.Equal("acme-pg", Assert.Single(targets.DatabaseMap).Key);
        Assert.Equal("ACME_HOME", targets.Desktop!.HomeEnv);
    }
}
