using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Schemas;

/// <summary>Update mode (AW_GOLDEN_UPDATE=1) on the schema check, run against a temp copy of schemas/evidence/ so the real tree is never written (#2320).</summary>
public sealed class EvidenceSchemaUpdateModeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-schema-" + RealTime.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory, Trait("Lane", "integration")]   // reads schemas/evidence/ and writes a temp copy
    [MemberData(nameof(EvidenceSchemaTests.Files), MemberType = typeof(EvidenceSchemaTests))]
    public void Update_mode_passes_when_the_file_already_matches(string name)
    {
        Seed(name);

        var failure = Record.Exception(() => EvidenceSchemaTests.Verify(name, _root, update: true, inCi: false));

        Assert.Null(failure);
    }

    [Fact, Trait("Lane", "integration")]   // reads schemas/evidence/ and writes a temp copy
    public void Update_mode_rewrites_a_stale_file_and_fails_once_so_the_diff_gets_reviewed()
    {
        Seed("api-script");
        var path = Path.Combine(_root, "schemas", "evidence", "api-script.schema.json");
        File.WriteAllText(path, "{}\n");

        var failure = Record.Exception(() => EvidenceSchemaTests.Verify("api-script", _root, update: true, inCi: false));

        Assert.NotNull(failure);
        Assert.Null(Record.Exception(() => EvidenceSchemaTests.Verify("api-script", _root, update: true, inCi: false)));
    }

    private void Seed(string name)
    {
        var dir = Path.Combine(_root, "schemas", "evidence");
        _ = Directory.CreateDirectory(dir);
        File.Copy(
            Path.Combine(EvidenceSchemaTests.RepoRoot(), "schemas", "evidence", $"{name}.schema.json"),
            Path.Combine(dir, $"{name}.schema.json"));
    }
}
