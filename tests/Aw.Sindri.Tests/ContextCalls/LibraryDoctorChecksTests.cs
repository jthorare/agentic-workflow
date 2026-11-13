using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Library;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Doctor's 'library' row (PR review page spec 2026-11-04 §4.1, §5.2): the roots' doc counts, a root that can't be read with its path and reason, and the near misses.</summary>
public sealed class LibraryDoctorChecksTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);
    private static readonly RepoEntry[] _repos = [new RepoEntry("split-panes", "/repos/split-panes")];
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LibraryRootStatus Ok(string root, int docs, int nearMisses = 0) => new(root, LibraryRootState.Ok, Reason: null, docs, nearMisses, _at, Path: null, NearMissFindings: []);

    private async Task<DoctorCheck> Row(params LibraryRootStatus[] roots)
    {
        await Approve();
        return await RowOf(FakeContextEngine.WithLibraryRoots(roots));
    }

    private async Task<DoctorCheck> RowOf(FakeContextEngine engine) =>
        Assert.Single(await new LibraryDoctorChecks(_h.Profiles, new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts()), engine).Run(Token));

    private async Task Approve()
    {
        var profile = new SindriProfile(1, "thor", Repos: _repos, Context: JsonDocument.Parse("""{"sources":{}}""").RootElement.Clone());
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_approved_profile_the_row_is_ok_and_not_configured()
    {
        var row = await RowOf(FakeContextEngine.Instance);

        Assert.Equal(("library", CheckState.Ok, "not configured (no profile yet)", null), (row.Name, row.State, row.Detail, row.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Roots_with_docs_are_ok_and_the_row_counts_them()
    {
        var row = await Row(Ok("aw", 2), Ok("work", 1));

        Assert.Equal((CheckState.Ok, "3 design docs in 2 roots (aw 2, work 1)", null), (row.State, row.Detail, row.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_doc_in_one_root_reads_in_the_singular()
    {
        var row = await Row(Ok("aw", 1));

        Assert.Equal("1 design doc in 1 root (aw 1)", row.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Before_the_first_pass_the_row_is_ok_and_says_the_read_hasnt_finished()
    {
        var row = await Row(new LibraryRootStatus("aw", LibraryRootState.Reading, Reason: null, 0, 0, LastRead: null, Path: null, NearMissFindings: []));

        Assert.Equal((CheckState.Ok, "the first read of aw hasn't finished yet (sindri library sync reads now)"), (row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_root_warns_with_its_path_and_reason_and_the_fix()
    {
        var row = await Row(Ok("aw", 2), new LibraryRootStatus("work", LibraryRootState.Unreadable, "/srv/work is not a folder that exists", 0, 0, _at, "/srv/work", NearMissFindings: []));

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Equal("work can't be read: /srv/work is not a folder that exists", row.Detail);
        Assert.Equal("mount or fix the folder, or take the vault out of context.sources.library.vaults; then sindri library sync", row.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Near_misses_warn_with_how_many_files_look_like_design_docs_but_arent_read()
    {
        var row = await Row(Ok("aw", 2, nearMisses: 1), Ok("work", 1, nearMisses: 2));

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Equal("3 files look like design docs but aren't read (aw 1, work 2)", row.Detail);
        Assert.Equal("write aw-doc: 1, unquoted, on the line after the first --- of each file, then sindri library sync", row.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_near_miss_reads_in_the_singular()
    {
        var row = await Row(Ok("aw", 2, nearMisses: 1));

        Assert.Equal("1 file looks like a design doc but isn't read (aw 1)", row.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_root_off_is_ok_and_says_how_to_turn_it_on()
    {
        var row = await Row(new LibraryRootStatus("aw", LibraryRootState.Off, Reason: null, 0, 0, LastRead: null, Path: null, NearMissFindings: []));

        Assert.Equal((CheckState.Ok, "off: context.sources.library.stateDir is off and no vault is named"), (row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_roots_read_that_fails_warns_with_its_code()
    {
        await Approve();

        var row = await RowOf(FakeContextEngine.WithLibraryRoots(ContextErrors.StoreUnavailable.With("the status file can't be read")));

        Assert.Equal(CheckState.Warn, row.State);
        Assert.StartsWith("SND-CONTEXT-", row.Detail, StringComparison.Ordinal);
    }
}
