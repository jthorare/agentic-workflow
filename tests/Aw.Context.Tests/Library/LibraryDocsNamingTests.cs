using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Library;

/// <summary>What <c language="text">IContextLibrary.DocsNaming</c> finds: the readable docs whose front-matter <c language="text">issues:</c> name Linear issues, per key, newest first (Linear hub spec 2026-11-04 §7).</summary>
[Trait("Lane", "integration")]
public sealed class LibraryDocsNamingTests : IDisposable
{
    private const string Body = "## Goals\n- **REQ-1** Book a ferry.\n";
    private static readonly SourceId _library = SourceId.From("library");
    private static readonly AudienceKey _repoAudience = AudienceKey.From("repo:acme/widgets");
    private static readonly AudienceKey _ownerAudience = AudienceKey.From("owner:library");
    private static readonly DateTimeOffset _t0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly RecordStoreWorld _world = new();
    private int _page;

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AudienceScope Session() => new(new HashSet<AudienceKey> { _repoAudience });

    private static AudienceScope Owner() => new(new HashSet<AudienceKey> { _repoAudience, _ownerAudience });

    private ContextLibrary Library => new(_world.Library);

    private async Task PutDoc(string key, string slug, string issues, int daysOld, AudienceKey? audience = null)
    {
        var parsed = DesignDocParser.Parse("/vault/" + slug + ".md", ADoc.Front("spec", slug, issues.Length == 0 ? string.Empty : $"issues: [{issues}]\n") + Body);
        var record = RecordSamples.Record(key, "v1", _library) with { Kind = RecordKind.Doc, Audience = audience ?? _repoAudience, UpdatedAt = _t0.AddDays(-daysOld) };
        _ = (await _world.Store.Commit(RecordSamples.Batch(_library, ++_page, RecordSamples.Write(record, "words") with { Side = new DesignDocSideRows(parsed) }), Ct)).Value;
    }

    private async Task<Dictionary<string, string[]>> Naming(AudienceScope audience, params string[] keys) =>
        (await Library.DocsNaming(keys, audience, Ct)).Value.ToDictionary(static p => p.Key, static p => p.Value.Select(static d => d.Slug ?? "").ToArray(), StringComparer.Ordinal);

    [Fact]
    public async Task Each_key_maps_to_the_docs_naming_it_newest_first_and_a_key_no_doc_names_is_absent()
    {
        await PutDoc("doc:aw/a.md", "older-plan", "ENG-12", daysOld: 10);
        await PutDoc("doc:aw/b.md", "newer-plan", "ENG-12, ENG-7", daysOld: 1);
        await PutDoc("doc:aw/c.md", "unrelated", "ENG-40", daysOld: 0);

        var named = await Naming(Session(), "ENG-12", "ENG-7", "ENG-99");

        Assert.Equal(["newer-plan", "older-plan"], named["ENG-12"]);
        Assert.Equal(["newer-plan"], named["ENG-7"]);
        Assert.False(named.ContainsKey("ENG-99"));
        Assert.Equal(2, named.Count);
    }

    [Fact]
    public async Task Lower_case_keys_in_the_ask_find_the_same_docs_under_the_upper_case_key()
    {
        await PutDoc("doc:aw/a.md", "plan", "ENG-12", daysOld: 1);

        var named = await Naming(Session(), "eng-12");

        Assert.Equal(["plan"], named["ENG-12"]);
    }

    [Fact]
    public async Task An_owner_only_doc_is_hidden_from_a_session_and_shown_to_the_owner()
    {
        await PutDoc("doc:aw/a.md", "shared-plan", "ENG-12", daysOld: 2);
        await PutDoc("doc:aw/private.md", "private-plan", "ENG-12", daysOld: 1, _ownerAudience);

        Assert.Equal(["shared-plan"], (await Naming(Session(), "ENG-12"))["ENG-12"]);
        Assert.Equal(["private-plan", "shared-plan"], (await Naming(Owner(), "ENG-12"))["ENG-12"]);
    }

    [Fact]
    public async Task More_than_two_hundred_keys_is_a_query_invalid_error_and_two_hundred_are_fine()
    {
        var many = Enumerable.Range(1, IContextLibrary.DocsNamingMaxKeys + 1).Select(static n => "ENG-" + n.ToString(CultureInfo.InvariantCulture)).ToList();

        var refused = await Library.DocsNaming(many, Session(), Ct);
        var fine = await Library.DocsNaming([.. many.Take(IContextLibrary.DocsNamingMaxKeys)], Session(), Ct);

        Assert.True(refused.IsError);
        Assert.Equal(ContextErrors.QueryInvalid.Code, refused.Error.Code);
        Assert.True(fine.IsSuccess);
    }
}
