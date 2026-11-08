using Aw.Kernel.Testing;

namespace Aw.Packets.Tests.Schemas;

/// <summary>Update mode (AW_GOLDEN_UPDATE=1) on the schema check, run against a temp copy of schemas/packets/ so the real tree is never written (#2320).</summary>
public sealed class PacketSchemaUpdateModeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-schema-" + RealTime.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory, Trait("Lane", "integration")]   // reads schemas/packets/ and writes a temp copy
    [MemberData(nameof(PacketSchemaTests.Files), MemberType = typeof(PacketSchemaTests))]
    public void Update_mode_passes_when_the_file_already_matches(string name)
    {
        Seed(name);

        var failure = Record.Exception(() => PacketSchemaTests.Verify(name, _root, update: true, inCi: false));

        Assert.Null(failure);
    }

    [Fact, Trait("Lane", "integration")]   // reads schemas/packets/ and writes a temp copy
    public void Update_mode_rewrites_a_stale_file_and_fails_once_so_the_diff_gets_reviewed()
    {
        Seed("packet");
        var path = Path.Combine(_root, "schemas", "packets", "api-script.schema.json");
        File.WriteAllText(path, "{}\n");

        var failure = Record.Exception(() => PacketSchemaTests.Verify("packet", _root, update: true, inCi: false));

        Assert.NotNull(failure);
        Assert.Null(Record.Exception(() => PacketSchemaTests.Verify("packet", _root, update: true, inCi: false)));
    }

    private void Seed(string name)
    {
        var dir = Path.Combine(_root, "schemas", "packets");
        _ = Directory.CreateDirectory(dir);
        File.Copy(
            Path.Combine(PacketSchemaTests.RepoRoot(), "schemas", "packets", $"{name}.schema.json"),
            Path.Combine(dir, $"{name}.schema.json"));
    }
}
