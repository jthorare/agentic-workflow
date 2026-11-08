using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.ProfileLive;

/// <summary>The old &lt;home&gt;/approved folder goes once, at the first start after the upgrade, with one ledger note (#2258 PR 1).</summary>
public sealed class ApprovalsDirectoryCleanupTests : IDisposable
{
    private const string NoteType = "profile.approvals.removed";   // guess: the fix may name the event otherwise

    private readonly TempHome _home = new();
    private readonly SliceHarness _slice = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task First_start_removes_the_old_approved_folder_once()
    {
        var settings = new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml"));
        var snapshot = Path.Combine(settings.ApprovalsDirectory, new string('a', 64));
        _ = Directory.CreateDirectory(snapshot);
        await File.WriteAllTextAsync(Path.Combine(snapshot, "profile.json"), "{}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(settings.ApprovalsDirectory, "latest"), new string('a', 64), TestContext.Current.CancellationToken);
        _ = await _slice.HoldLease();
        using var caller = _slice.Callers.EnterService("test");
        var cleanup = new ApprovalsDirectoryCleanup(settings, _slice.Held, _slice.Store, _slice.Events);
        var token = TestContext.Current.CancellationToken;

        Assert.True((await cleanup.Run(token)).IsSuccess);
        Assert.True((await cleanup.Run(token)).IsSuccess);   // the second start finds nothing to do

        Assert.False(Directory.Exists(settings.ApprovalsDirectory));
        _ = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == NoteType);
    }
}
