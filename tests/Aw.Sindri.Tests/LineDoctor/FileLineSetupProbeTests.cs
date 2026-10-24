using Aw.Sindri.Infrastructure.Line;

namespace Aw.Sindri.Tests.LineDoctor;

/// <summary>The real probe over a temp checkout: the test-path convention, the evidence targets file and the credential variable.</summary>
public sealed class FileLineSetupProbeTests : IDisposable
{
    private readonly string _checkout = Directory.CreateTempSubdirectory("aw-line-doctor-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_checkout, recursive: true);

    private void Touch(string relative)
    {
        var path = Path.Combine(_checkout, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Probe_finds_a_test_file_the_evidence_targets_and_the_credential_variable()
    {
        Touch("src/App.cs");
        Touch("tests/AppTests.cs");
        Touch(".agentic-workflow/evidence.json");

        var facts = await new FileLineSetupProbe(name => name == "AW_FORGE_TOKEN" ? "x" : null).Read(_checkout, "AW_FORGE_TOKEN", Token);

        Assert.True(facts.TestFiles);
        Assert.True(facts.EvidenceConfig);
        Assert.True(facts.CredentialSet);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Probe_ignores_tests_under_dependency_folders_and_reports_a_command_credential_as_unknown()
    {
        Touch("src/App.cs");
        Touch("node_modules/pkg/tests/x.js");

        var facts = await new FileLineSetupProbe(static _ => null).Read(_checkout, credentialVariable: null, Token);

        Assert.False(facts.TestFiles);
        Assert.False(facts.EvidenceConfig);
        Assert.Null(facts.CredentialSet);
    }
}
