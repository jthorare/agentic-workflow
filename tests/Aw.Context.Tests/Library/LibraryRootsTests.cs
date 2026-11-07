using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Library;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Library;

/// <summary>Where each library root stands, as the doctor row and the Library page read it (PR review page spec 2026-11-04 §4.1, §5.2): the owner's, from what the feed's last pass left.</summary>
public sealed class LibraryRootsTests : IDisposable
{
    private const string WithVault = """{"sources":{"library":{"vaults":[{"name":"work","path":"/srv/work"}]}}}""";

    private static readonly DateTimeOffset _at = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    private readonly RecordStoreWorld _world = new();
    private readonly FakeLibraryStatus _status = new();

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ContextLibrary Library => new(_world.Library, _status);

    private static DesignDocFinding Miss(string path, int line) =>
        new(path, new DesignDocError(DesignDocCodes.NearMiss, line, string.Create(CultureInfo.InvariantCulture, $"{path}:{line}: aw-doc is \"1\", not the number 1, so the file isn't read."), "Write aw-doc: 1 with no quotes, on the line after the first ---."));

    [Fact]
    public async Task Before_a_pass_has_finished_every_configured_root_is_reading()
    {
        var roots = (await Library.Roots(MapWorld.HumanWith(WithVault), Ct)).Value;

        Assert.Equal([("aw", LibraryRootState.Reading), ("work", LibraryRootState.Reading)], roots.Select(static r => (r.Root, r.State)));
        Assert.All(roots, static r => Assert.Null(r.LastRead));
    }

    [Fact]
    public async Task A_finished_pass_gives_ok_with_its_counts()
    {
        _status.Write([new LibraryRootReport("work", "/srv/work", Problem: null, Docs: 3, [Miss("plans/a.md", 2), Miss("plans/b.md", 3)], _at)]);

        var work = (await Library.Roots(MapWorld.HumanWith(WithVault), Ct)).Value.Single(static r => r.Root == "work");

        Assert.Equal((LibraryRootState.Ok, 3, 2, "/srv/work"), (work.State, work.Docs, work.NearMisses, work.Path));
        Assert.Null(work.Reason);
        Assert.Equal(_at, work.LastRead);
        Assert.Equal([Miss("plans/a.md", 2), Miss("plans/b.md", 3)], work.NearMissFindings);
    }

    [Fact]
    public async Task Before_a_pass_has_finished_a_root_has_no_path_and_no_findings()
    {
        var roots = (await Library.Roots(MapWorld.HumanWith(WithVault), Ct)).Value;

        Assert.All(roots, static r => Assert.Null(r.Path));
        Assert.All(roots, static r => Assert.Empty(r.NearMissFindings));
    }

    [Fact]
    public async Task An_unreadable_root_carries_the_reason_the_pass_gave()
    {
        _status.Write([new LibraryRootReport("work", "/srv/work", "/srv/work is not a folder that exists", Docs: 0, [], _at)]);

        var work = (await Library.Roots(MapWorld.HumanWith(WithVault), Ct)).Value.Single(static r => r.Root == "work");

        Assert.Equal(LibraryRootState.Unreadable, work.State);
        Assert.Equal("/srv/work is not a folder that exists", work.Reason);
        Assert.Equal("/srv/work", work.Path);
    }

    [Fact]
    public async Task The_aw_root_turned_off_is_off_and_a_report_for_a_root_no_longer_configured_is_ignored()
    {
        _status.Write([new LibraryRootReport("gone", "/srv/gone", Problem: null, Docs: 1, [], _at)]);

        var roots = (await Library.Roots(MapWorld.HumanWith("""{"sources":{"library":{"stateDir":"off"}}}"""), Ct)).Value;

        var aw = Assert.Single(roots);
        Assert.Equal(("aw", LibraryRootState.Off, 0, 0), (aw.Root, aw.State, aw.Docs, aw.NearMisses));
        Assert.Null(aw.Reason);
        Assert.Null(aw.LastRead);
        Assert.Null(aw.Path);
        Assert.Empty(aw.NearMissFindings);
    }

    [Fact]
    public async Task A_profile_with_no_library_key_still_has_the_aw_root()
    {
        var roots = (await Library.Roots(MapWorld.HumanWith("{}"), Ct)).Value;

        Assert.Equal("aw", Assert.Single(roots).Root);
    }

    [Fact]
    public async Task A_session_is_refused()
    {
        var result = await Library.Roots(MapWorld.Session("aw-split-panes"), Ct);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.SessionsDisabled.Code, result.Error.Code);
    }
}
