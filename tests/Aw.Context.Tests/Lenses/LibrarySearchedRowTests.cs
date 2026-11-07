using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Library;
using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;

namespace Aw.Context.Tests.Lenses;

/// <summary>An answer's library row (issue 2165): only the owner's answer lists it, and it is not-configured only when no root is left, since the 'aw' root is read by default.</summary>
public sealed class LibrarySearchedRowTests
{
    private static readonly DateTimeOffset _synced = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly SourceProgress _docs = SourceProgress.From(SourceId.From("repo-docs"), SyncPhase.Incremental, _synced, lastCode: null);
    private static readonly SourceProgress _neverSynced = SourceProgress.From(LibrarySource.Id, phase: null, lastSync: null, lastCode: null);
    private static readonly SourceProgress _librarySynced = SourceProgress.From(LibrarySource.Id, SyncPhase.Incremental, _synced, lastCode: null);

    [Theory, Trait("Lane", "unit")]
    [InlineData(ContextCallerKind.Session)]
    [InlineData(ContextCallerKind.Service)]
    public void A_caller_that_is_not_the_owner_gets_no_library_row(ContextCallerKind caller)
    {
        var rows = LensBasisReader.IndexRows([_docs, _librarySynced], library: null, caller);

        Assert.Equal([SourceId.From("repo-docs")], rows.Select(r => r.Id));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_owner_with_no_library_block_gets_the_aw_roots_row_not_synced_before_its_first_pass()
    {
        var row = LibraryRow(LensBasisReader.IndexRows([_docs, _neverSynced], library: null, ContextCallerKind.Human));

        Assert.Equal((SearchedState.Unavailable, "not-synced", ContextErrors.IntentNotSynced.Code), (row.State, row.Reason, row.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_owner_with_the_aw_root_off_and_no_vaults_gets_not_configured()
    {
        var library = new LibrarySettings { StateDir = LibrarySettings.Off };

        var row = LibraryRow(LensBasisReader.IndexRows([_docs, _librarySynced], library, ContextCallerKind.Human));

        Assert.Equal((SearchedState.NotConfigured, "not-configured", null), (row.State, row.Reason, row.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_owner_with_the_aw_root_off_but_a_vault_gets_the_index_row()
    {
        var library = new LibrarySettings { StateDir = LibrarySettings.Off, Vaults = [new LibraryVault { Name = "notes", Path = "/vault" }] };

        var row = LibraryRow(LensBasisReader.IndexRows([_librarySynced], library, ContextCallerKind.Human));

        Assert.Equal((SearchedState.Ok, _synced), (row.State, row.AsOf));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_owner_with_a_synced_library_gets_the_synced_row()
    {
        var row = LibraryRow(LensBasisReader.IndexRows([_docs, _librarySynced], library: null, ContextCallerKind.Human));

        Assert.Equal((SearchedState.Ok, null, _synced), (row.State, row.Code, row.AsOf));
    }

    private static SearchedSource LibraryRow(IReadOnlyList<SearchedSource> rows) => Assert.Single(rows, r => r.Id == LibrarySource.Id);
}
