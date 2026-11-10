using Aw.Kernel.Testing;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Resources;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Resources;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
[Lane(TestLane.Integration)]
public sealed class ResourceProvisionerTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly LedgerDatabase _ledger;
    private readonly ResourceProvisioner _provisioner;

    public ResourceProvisionerTests()
    {
        _ledger = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _clock);
        _provisioner = new ResourceProvisioner(new ResourceStore(Root), _ledger, _clock);
    }

    public void Dispose()
    {
        _ledger.Dispose();
        _home.Dispose();
    }

    private static ResourceOwner Owner => ResourceOwner.Parse("workflow:triage").Value;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_home.Path, "resources");

    [Fact]
    public async Task Ensure_creates_the_database_and_books_one_row_with_the_env_and_the_mount()
    {
        var spec = new ResourceSpec([new DatabaseResource("state")], [], Sandbox: null, []);

        var result = await _provisioner.Ensure(Owner, spec, Token);

        var db = Path.Combine(Root, "workflow", "triage", "state.db");
        Assert.True(File.Exists(db));
        Assert.Equal(db, result.Value.Env["SINDRI_DB_STATE"]);
        Assert.Equal(Path.Combine(Root, "workflow", "triage"), Assert.Single(result.Value.Mounts).HostPath);
        var row = Assert.Single((await _provisioner.Booked(Owner, Token)).Value);
        Assert.Equal(("workflow:triage", "database", "state", db), (row.Owner, row.Kind, row.Name, row.Path));
    }

    [Fact]
    public async Task Ensure_twice_keeps_one_row_per_resource_and_the_data()
    {
        var spec = new ResourceSpec([new DatabaseResource("state")], [new DiskResource("scratch", "1MiB")], Sandbox: null, []);
        _ = await _provisioner.Ensure(Owner, spec, Token);
        await File.WriteAllTextAsync(Path.Combine(Root, "workflow", "triage", "scratch", "keep.txt"), "kept", Token);

        var again = await _provisioner.Ensure(Owner, spec, Token);

        Assert.True(again.IsSuccess);
        Assert.Equal(2, (await _provisioner.Booked(Owner, Token)).Value.Count);
        Assert.Equal("kept", await File.ReadAllTextAsync(Path.Combine(Root, "workflow", "triage", "scratch", "keep.txt"), Token));
    }

    [Fact]
    public async Task An_over_size_disk_fails_the_run_with_used_and_limit_numbers()
    {
        var spec = new ResourceSpec([], [new DiskResource("scratch", "1KiB")], Sandbox: null, []);
        _ = await _provisioner.Ensure(Owner, spec, Token);
        await File.WriteAllBytesAsync(Path.Combine(Root, "workflow", "triage", "scratch", "big.bin"), new byte[4096], Token);

        var result = await _provisioner.Ensure(Owner, spec, Token);

        Assert.Equal("SND-RES-001", result.Error.Code);
        Assert.Contains("4096", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("1024", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bad_declaration_books_nothing()
    {
        var spec = new ResourceSpec([new DatabaseResource("Bad Name")], [], Sandbox: null, []);

        var result = await _provisioner.Ensure(Owner, spec, Token);

        Assert.Equal("SND-RES-006", result.Error.Code);
        Assert.Empty((await _provisioner.Booked(Owner, Token)).Value);
    }
}
