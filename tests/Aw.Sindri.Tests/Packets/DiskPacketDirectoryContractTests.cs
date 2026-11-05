using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The disk packet files honour the packet-files contract.</summary>
[Lane(TestLane.Integration)]
public sealed class DiskPacketDirectoryContractTests : PacketDirectoryContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override string Nothing => Path.Combine(_home.Path, "no-such-packet");

    protected override string Seed(IPacketDirectory files, IReadOnlyDictionary<string, string> tree)
    {
        var directory = Path.Combine(_home.Path, "seeded-packet");
        foreach (var (relative, text) in tree)
        {
            var path = Path.Combine(directory, relative);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a path built from a directory and a relative file always has a parent
            File.WriteAllText(path, text);
        }

        return directory;
    }

    [Fact]
    public async Task Only_the_top_level_and_the_model_folder_are_listed_not_every_nested_file()
    {
        var directory = Seed(Create(), new Dictionary<string, string>(StringComparer.Ordinal) { ["phase.json"] = "{}", ["evidence/shot.png"] = "x", ["model/Retry/Retry.tla"] = "m" });

        var read = await Create().Read(directory, TestContext.Current.CancellationToken);

        Assert.Equal(["model/Retry/Retry.tla", "phase.json"], read.Value.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    protected override IPacketDirectory Create() => new DiskPacketDirectory(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")));
}
