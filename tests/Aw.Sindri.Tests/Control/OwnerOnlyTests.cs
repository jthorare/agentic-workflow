using System.Runtime.Versioning;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Control;

[UnsupportedOSPlatform("windows")]
public sealed class OwnerOnlyTests : IDisposable
{
    private const UnixFileMode Owner = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public void A_directory_it_creates_is_0700()
    {
        var created = Path.Combine(_home.Path, "new");

        OwnerOnly.EnsureDirectory(created);

        Assert.Equal(Owner, File.GetUnixFileMode(created));
    }

    [Fact, Trait("Lane", "integration")]
    public void An_existing_directory_keeps_the_mode_its_owner_set()
    {
        var shared = Path.Combine(_home.Path, "shared");
        _ = Directory.CreateDirectory(shared);
        File.SetUnixFileMode(shared, Owner | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);

        OwnerOnly.EnsureDirectory(shared);

        Assert.Equal(Owner | UnixFileMode.GroupRead | UnixFileMode.GroupExecute, File.GetUnixFileMode(shared));
    }
}
