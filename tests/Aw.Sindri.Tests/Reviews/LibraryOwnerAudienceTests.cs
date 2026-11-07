using System.Text.Json;
using Aw.Context.Core.Audience;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ReviewListFacts;
using Aw.Sindri.Tests.Workflow.Blocks.Input;
using RecordKey = Aw.Context.Core.Sources.RecordKey;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>A vault doc that names no single synced repo is stored as 'owner:library'; the library page shows it to a human and hides it from a session (#2176).</summary>
[Trait("Lane", "unit")]
public sealed class LibraryOwnerAudienceTests
{
    private const string Context = """{"sources":{"github":{"repos":["acme/api"]},"library":{"vaults":[{"name":"work","path":"~/Documents/Work"}]}}}""";
    private static readonly LibraryHost _host = new() { Home = "/home/joi", StateDefault = "/home/joi/.agentic-workflow" };
    private readonly SliceHarness _h = new();
    private readonly Stub _context = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task Approve()
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("api", "/home/joi/repos/api")], Context: JsonDocument.Parse(Context).RootElement.Clone())).Value;
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Ct);
    }

    private ContextLibraryPageFacts Facts() => new(_h.Profiles, _h.Callers, EngineProxy.Of(_context), _host);

    private static LibraryDoc OwnerDoc(int errors) =>
        new(RecordKey.From("doc:work/notes.md"), "work", "work", Slug: "notes.md", Kind: null, Status: null, Project: null, [], [], ParserVersion: 1,
            [.. Enumerable.Range(0, errors).Select(static _ => new DesignDocError("SND-LIB-010", Line: 3, "m", "f"))], "notes.md", Url: null, Audience: AudienceKey.From(ProfileAudiencePolicy.OwnerLibrary));

    [Fact]
    public async Task A_human_sees_a_vault_doc_stored_as_owner_library_and_its_errors()
    {
        await Approve();
        using var caller = _h.Callers.EnterService("library-test");
        _context.Docs = [OwnerDoc(errors: 1)];

        var facts = (await Facts().Read(Ct)).Value;

        Assert.Equal(1, facts.Docs);
        Assert.Equal("notes.md", Assert.Single(facts.Broken).Doc);
    }

    [Fact]
    public async Task A_session_does_not_see_a_vault_doc_stored_as_owner_library()
    {
        await Approve();
        using var caller = _h.Callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, "session:mcp", _h.Ids.New<CommandId>(), _h.Ids.New<CorrelationId>(), RepoName.From("api")));
        _context.Docs = [OwnerDoc(errors: 1)];

        var facts = (await Facts().Read(Ct)).Value;

        Assert.Equal(0, facts.Docs);
        Assert.Empty(facts.Broken);
    }

    /// <summary>Answers the library reads the way Context does: only the docs the caller's audience allows.</summary>
    private sealed class Stub : IContextLibrary, IIndexSync
    {
        public IReadOnlyList<LibraryDoc> Docs { get; set; } = [];

        public ValueTask<Result<IReadOnlyList<LibraryDoc>>> ListDocs(LibraryDocFilter filter, AudienceScope audience, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<IReadOnlyList<LibraryDoc>>([.. Docs.Where(d => audience.Allows(d.Audience, d.Key))]));

        public ValueTask<Result<LibraryDoc?>> GetDoc(RecordKey key, AudienceScope audience, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(Docs.FirstOrDefault(d => d.Key == key && audience.Allows(d.Audience, d.Key))));

        ValueTask<Result<IReadOnlyList<LibraryRootStatus>>> IContextLibrary.Roots(ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<IReadOnlyList<LibraryRootStatus>>([]));

        ValueTask<Result<IReadOnlyList<SourceProgress>>> IIndexSync.Progress(ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<IReadOnlyList<SourceProgress>>([]));

        public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryRefResolution>>> Resolve(IReadOnlyList<string> refs, string? repo, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> Claims(RecordKey prKey, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyDictionary<RecordKey, IReadOnlyList<LibraryClaim>>>> Claims(IReadOnlyList<RecordKey> prKeys, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> ClaimsOn(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SyncReport>> Tick(ContextScope scope, SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<ConceptStatus>> ConceptStatus(ContextScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
