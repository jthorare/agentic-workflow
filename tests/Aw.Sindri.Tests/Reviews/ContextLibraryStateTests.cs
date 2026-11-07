using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Sindri.Features.ReviewListFacts;
using Aw.Sindri.Tests.Workflow.Blocks.Input;
using RecordKey = Aw.Context.Core.Sources.RecordKey;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The Reviews header's library line over Context's library reads (PR review page spec §6.1, §6.3): the docs, the unreadable folders written as the profile writes them, and a first read still running.</summary>
[Trait("Lane", "unit")]
public sealed class ContextLibraryStateTests
{
    private static readonly LibraryHost _host = new() { Home = "/home/joi", StateDefault = "/home/joi/.agentic-workflow" };
    private static readonly ContextSettings _settings = new() { Library = new LibrarySettings { Vaults = [new LibraryVault { Name = "vault", Path = "~/notes/vault" }] } };
    private static readonly ContextScope _scope = new(Settings: null, [], new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http));
    private readonly StubLibrary _library = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Aw.Sindri.Core.Reviews.ReviewLibraryFacts> Read(SourceProgress? source = null) =>
        await new ContextLibraryState(EngineProxy.Of(_library), _host).Read(_settings, _scope, new AudienceScope(new HashSet<AudienceKey>()), source, Ct);

    private static LibraryDoc Doc(string path, int errors) =>
        new(RecordKey.From("doc:vault/" + path), "vault", "vault", Slug: path, Kind: null, Status: null, Project: null, [], [], ParserVersion: 1,
            [.. Enumerable.Range(0, errors).Select(static _ => new DesignDocError("SND-LIB-010", Line: 3, "m", "f"))], path, Url: null, Audience: null);

    private static LibraryRootStatus Root(LibraryRootState state, string? reason = null) => new("vault", state, reason, Docs: 0, NearMisses: 0, LastRead: null);

    [Fact]
    public async Task The_docs_and_the_docs_with_errors_are_counted()
    {
        _library.Docs = [Doc("a.md", 0), Doc("b.md", 2), Doc("c.md", 1)];

        var facts = await Read();

        Assert.Equal((3, 2, false, 0), (facts.Docs, facts.WithErrors, facts.Reading, facts.UnreadableRoots.Count));
    }

    [Fact]
    public async Task An_unreadable_root_is_named_by_its_folder_with_the_home_as_a_tilde_and_its_reason()
    {
        _library.RootStatuses = [Root(LibraryRootState.Ok), Root(LibraryRootState.Unreadable, "permission denied")];

        var facts = await Read();

        Assert.Equal([new Aw.Sindri.Core.Reviews.ReviewUnreadableRoot("~/notes/vault", "permission denied")], facts.UnreadableRoots);
    }

    [Fact]
    public async Task A_root_still_reading_makes_the_header_read()
    {
        _library.RootStatuses = [Root(LibraryRootState.Reading)];

        var facts = await Read();

        Assert.True(facts.Reading);
    }

    [Fact]
    public async Task A_session_is_refused_the_roots_and_the_header_shows_none()
    {
        _library.RootsFailure = ContextErrors.SessionsDisabled.With("sessions are off");
        _library.Docs = [Doc("a.md", 0)];

        var facts = await Read();

        Assert.Equal((1, false, 0), (facts.Docs, facts.Reading, facts.UnreadableRoots.Count));
    }

    /// <summary>Answers the library reads as set; every other call falls through to the fake engine.</summary>
    private sealed class StubLibrary : IContextLibrary
    {
        public IReadOnlyList<LibraryDoc> Docs { get; set; } = [];

        public IReadOnlyList<LibraryRootStatus> RootStatuses { get; set; } = [];

        public Error? RootsFailure { get; set; }

        public ValueTask<Result<IReadOnlyList<LibraryDoc>>> ListDocs(LibraryDocFilter filter, AudienceScope audience, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Docs));

        ValueTask<Result<IReadOnlyList<LibraryRootStatus>>> IContextLibrary.Roots(ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult(RootsFailure is { } failure ? (Result<IReadOnlyList<LibraryRootStatus>>)failure : Results.Success(RootStatuses));

        public ValueTask<Result<LibraryDoc?>> GetDoc(RecordKey key, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryItem>>> Items(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryRefResolution>>> Resolve(IReadOnlyList<string> refs, string? repo, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> Claims(RecordKey prKey, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<LibraryClaim>>> ClaimsOn(RecordKey docKey, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
