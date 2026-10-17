using System.Text.Json;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Launch.Config;

/// <summary>The C# reader gives every fixture in tests/fixtures/launcher/manifest.json the verdict Ajv gives it (scripts/check-launcher.mjs enforces Ajv's).</summary>
public sealed class ManifestParityTests
{
    public static TheoryData<string, bool> Fixtures
    {
        get
        {
            var data = new TheoryData<string, bool>();
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(SessionFixtures.Launcher, "manifest.json")));
            foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
            {
                data.Add(fixture.GetProperty("file").GetString()!, fixture.GetProperty("schema").GetString() == "valid");
            }

            return data;
        }
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Fixtures))]
    public void Reader_matches_every_fixture_verdict_in_the_manifest(string file, bool valid)
    {
        var read = LauncherConfigFile.Read(SessionFixtures.Builtins(), File.ReadAllBytes(Path.Combine(SessionFixtures.Launcher, file)));

        Assert.True(read.IsSuccess == valid, $"{file}: expected {(valid ? "valid" : "invalid")}, got {(read.IsError ? read.Error.Message : "valid")}");
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_fixture_file_is_in_the_manifest()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(SessionFixtures.Launcher, "manifest.json")));
        var listed = manifest.RootElement.GetProperty("fixtures").EnumerateArray().Select(static f => f.GetProperty("file").GetString()).ToHashSet(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(SessionFixtures.Launcher, "*.json").Select(Path.GetFileName).Where(static f => f != "manifest.json");

        foreach (var file in files)
        {
            Assert.Contains(file, listed);
        }
    }
}
