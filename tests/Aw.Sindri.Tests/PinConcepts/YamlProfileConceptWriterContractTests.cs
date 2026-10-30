using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.PinConcepts;

[Lane(TestLane.Integration)]
public sealed class YamlProfileConceptWriterContractTests : ProfileConceptWriterContract, IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "aw-concept-writer-contract-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    protected override (IProfileConceptWriter Writer, ProfileHash Hash) Create()
    {
        _ = Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "profile.yaml");
        File.WriteAllText(path, "schemaVersion: 1\nuser: thor\nrepos: []\n");
        Assert.Null(YamlToJson.TryConvert(File.ReadAllText(path), out var json));
        var loaded = ProfileReading.FromJson(json, path, ProfileFiles.Options);
        return (new YamlProfileConceptWriter(new SindriSettings(_dir, path), ProfileFiles.Options), loaded.Value.Hash);
    }
}
