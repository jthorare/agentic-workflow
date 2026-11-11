using Aw.Redaction;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Infrastructure.Text;

namespace Aw.Sindri.Tests.Checks;

/// <summary>#2644: the value shape that lets the staged scan treat a secret-assignment as a pointer.</summary>
public sealed class ResourceNameTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("demo-background-effect-cluster-v2-env")]
    [InlineData("uat-worker-queue-cluster-v20-env")]
    [InlineData("worker-env1-config")]
    [InlineData("demo-worker-queue")]
    public void Looks_is_true_for_a_lowercase_dashed_name_with_digits_only_as_a_short_suffix(string value) =>
        Assert.True(ResourceName.Looks(value));

    [Theory, Trait("Lane", "unit")]
    [InlineData("q7x2k9m4p1z8w3n6b5v0c7a2")]
    [InlineData("q7x2k-9m4p1-z8w3n-6b5v0-c7a2")]
    [InlineData("Demo-Background-v2-env")]
    [InlineData("abc-12345678-def")]
    [InlineData("abc-123-def")]
    [InlineData("worker-v2000-config")]
    [InlineData("demo-worker+queue-v2")]
    [InlineData("demo-worker/queue-v2")]
    [InlineData("demo-worker=queue-v2")]
    [InlineData("demo_worker-queue-v2")]
    [InlineData("demo--worker-v2")]
    [InlineData("-demo-worker-v2")]
    [InlineData("demo-worker-v2-")]
    [InlineData("2demo-worker-v2")]
    [InlineData("")]
    public void Looks_is_false_for_anything_that_could_be_a_credential(string value) =>
        Assert.False(ResourceName.Looks(value));

    [Fact, Trait("Lane", "unit")]
    public void Findings_still_flags_a_line_with_a_name_shaped_assignment_and_a_token_of_another_rule()
    {
        var token = string.Concat("g", "hp", "_", new string('a', 18), "1B2c3D4e5F6g7H8i9J");
        var line = string.Concat("ESO_", "SECRET=", "demo-background-effect-cluster-v2-env ", token);
        var finder = new RedactingSecretFinder(Redactor.Default);

        var found = SecretScan.Findings("X-1", [new AddedLine("a.yaml", 3, line)], finder);

        _ = Assert.Single(found);
    }

    [Fact, Trait("Lane", "unit")]
    public void Findings_passes_a_line_holding_only_a_name_shaped_assignment()
    {
        var finder = new RedactingSecretFinder(Redactor.Default);
        var line = string.Concat("ESO_", "SECRET=", "demo-background-effect-cluster-v2-env");

        var found = SecretScan.Findings("X-1", [new AddedLine("a.yaml", 3, line)], finder);

        Assert.Empty(found);
    }
}
