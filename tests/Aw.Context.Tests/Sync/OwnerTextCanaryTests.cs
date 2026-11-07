using System.Text.Json;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Library;
using Aw.Context.Core.Models;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Features.ReadLibrary;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Tests.Infrastructure;
using Aw.Context.Tests.SynthesizeConcepts;

namespace Aw.Context.Tests.Sync;

/// <summary>
/// A canary for the one rule the library must not break (PR review page spec 2026-11-04 D13, §5.4): text of an <c language="text">owner:library</c> note, in a vault whose <c language="text">models: true</c>,
/// reaches nothing a session can see. The note holds a unique string; every read a session makes is serialized and searched for it, every model port echoes its prompt into its answer so a leak would carry it,
/// and the owner's own read must still hold it, which proves the string really is in the store.
/// </summary>
[Trait("Lane", "integration")]
public sealed class OwnerTextCanaryTests
{
    private const string Canary = "canary-7f3a91-harbor";
    private const string ShopText = "shop-visible-5c21-harbor";

    private static readonly AudienceKey _shop = ProfileAudiencePolicy.LibraryDoc(["acme/shop"], ["acme/shop"]);
    private static readonly AudienceKey _owner = AudienceKey.From("owner:library");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Note(string id, string body, string repos = "") =>
        $"---\naw-doc: 1\nid: {id}\nkind: plan\ntitle: \"{id}\"\nstatus: draft\n{repos}---\n\n# {id}\n\n## Goals\n- **REQ-1** {body}\n";

    private static string Json(object? read) => JsonSerializer.Serialize(read);

    [Fact]
    public async Task A_sessions_library_reads_never_carry_the_owner_note_while_the_owners_do()
    {
        using var home = new TempHome();
        using var rig = new LibraryFeedRig(home, copyFixture: false);
        _ = rig.Write("plans/owner-note.md", Note("owner-note", Canary));
        _ = rig.Write("plans/shop-note.md", Note("shop-note", ShopText, "repos: [acme/shop]\n"));
        _ = await FeedRun.Once(rig.FeedSyncing(["acme/shop"], rig.Vault()), rig.Store, home.Scrubbers);
        var library = new ContextLibrary(new SqliteLibraryStore(home.Database));
        var session = new AudienceScope(new HashSet<AudienceKey> { _shop });
        var owner = new AudienceScope(new HashSet<AudienceKey> { _shop, _owner });
        var ownerKey = RecordKey.From("doc:harbor/plans/owner-note.md");
        var shopKey = RecordKey.From("doc:harbor/plans/shop-note.md");

        async Task<string> Reads(AudienceScope who) => string.Join('\n',
            Json((await library.ListDocs(new LibraryDocFilter(), who, Ct)).Value),
            Json((await library.GetDoc(ownerKey, who, Ct)).Value),
            Json((await library.Items(ownerKey, who, Ct)).Value),
            Json((await library.Items(shopKey, who, Ct)).Value),
            Json((await library.ClaimsOn(ownerKey, who, Ct)).Value),
            Json((await library.Resolve(["doc:harbor/owner-note#REQ-1", "owner-note#REQ-1"], "acme/shop", who, Ct)).Value));

        var asSession = await Reads(session);
        var asOwner = await Reads(owner);

        Assert.DoesNotContain(Canary, asSession, StringComparison.Ordinal);
        Assert.Contains(ShopText, asSession, StringComparison.Ordinal);   // the session's reads do work
        Assert.Contains(Canary, asOwner, StringComparison.Ordinal);   // and the string really is in the store
    }

    [Fact]
    public async Task Concept_synthesis_with_models_true_never_shows_the_owner_note_to_a_model_or_writes_it_into_a_concept()
    {
        var withOwnerDoc = await Synthesize(_owner);
        var withRepoDoc = await Synthesize(_shop);

        Assert.DoesNotContain(withOwnerDoc.Prompts, static p => p.Contains(Canary, StringComparison.Ordinal));
        Assert.DoesNotContain(Canary, withOwnerDoc.Concepts, StringComparison.Ordinal);
        Assert.Contains(withRepoDoc.Prompts, static p => p.Contains(Canary, StringComparison.Ordinal));   // the same note under a repo audience does reach the model
        Assert.Contains(Canary, withRepoDoc.Concepts, StringComparison.Ordinal);   // and the echo carries it into the concept, so a leak would show
    }

    private static async Task<(List<string> Prompts, string Concepts)> Synthesize(AudienceKey noteAudience)
    {
        var rig = new SynthesisRig();
        rig.Model.Responder = request => SynthesisRig.Json("Harbor echo", JsonSerializer.Serialize(request.Prompt)[1..^1]);   // the model echoes what it was shown
        await rig.Add("linear", "linear:issue/ENG-1", 0);
        await rig.Add("github", "github:pr/acme/shop/2", 5);
        await rig.Add("library", "doc:harbor/plans/owner-note.md", 3, text: "Pane layout " + Canary, audience: noteAudience.Value);
        _ = await rig.Run(libraryRoots: ["harbor"]);
        return ([.. rig.Model.Requests.Where(static r => r.Purpose == ModelPurpose.ConceptSynthesis).Select(static r => r.Prompt)], Json((await rig.Concepts.All(Ct)).Value));
    }
}
