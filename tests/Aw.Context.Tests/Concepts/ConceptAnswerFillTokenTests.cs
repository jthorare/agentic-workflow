using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Concepts;

/// <summary>
/// Issue 4176: the publish fill hands the token it was given (the host's stop) to every store call, so a host's stop waits only as long as a store
/// that heeds the token, never for a store call that was never told.
/// </summary>
[Trait("Lane", "unit")]
public sealed class ConceptAnswerFillTokenTests : IDisposable
{
    private readonly TokenRecordingStore _recorder = new();
    private readonly MapWorld _world;

    public ConceptAnswerFillTokenTests()
    {
        _world = new MapWorld(configure: s => s.AddSingleton<IConceptAnswerStore>(_recorder));
        _recorder.Inner = _world.Answers;
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task A_fill_hands_its_token_to_the_concepts_and_the_glossary_store_calls()
    {
        _ = await _world.ImportFixture();   // its own fill keeps the rows, so the fill below reads them back
        _recorder.Calls.Clear();
        using var stop = new CancellationTokenSource();

        await _world.Service<ConceptAnswers>().Fill(MapWorld.HumanVia(ContextClient.Http), stop.Token);

        Assert.Contains(_recorder.Calls, c => string.Equals(c.Call, nameof(IConceptAnswerStore.Concepts), StringComparison.Ordinal));
        Assert.Contains(_recorder.Calls, c => string.Equals(c.Call, nameof(IConceptAnswerStore.Glossary), StringComparison.Ordinal));
        Assert.All(_recorder.Calls, c => Assert.Equal(stop.Token, c.Token));
    }

    // The world's store, with each call and the token it was given written down.
    private sealed class TokenRecordingStore : IConceptAnswerStore
    {
        public IConceptAnswerStore? Inner { get; set; }

        public List<(string Call, CancellationToken Token)> Calls { get; } = [];

        private IConceptAnswerStore Store => Inner ?? throw new InvalidOperationException("the recording store has no inner store yet");

        public ValueTask<Result<ConceptAnswerBasis?>> Basis(MapRead read, CancellationToken cancellationToken) =>
            Seen(nameof(Basis), cancellationToken).Basis(read, cancellationToken);

        public ValueTask<Result<KeptConcepts?>> Concepts(string key, CancellationToken cancellationToken) =>
            Seen(nameof(Concepts), cancellationToken).Concepts(key, cancellationToken);

        public ValueTask<Result<ConceptGlossaryView?>> Glossary(string key, CancellationToken cancellationToken) =>
            Seen(nameof(Glossary), cancellationToken).Glossary(key, cancellationToken);

        public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, KeptConcepts view, CancellationToken cancellationToken) =>
            Seen(nameof(Keep), cancellationToken).Keep(key, basis, view, cancellationToken);

        public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, ConceptGlossaryView view, CancellationToken cancellationToken) =>
            Seen(nameof(Keep), cancellationToken).Keep(key, basis, view, cancellationToken);

        private IConceptAnswerStore Seen(string call, CancellationToken token)
        {
            Calls.Add((call, token));
            return Store;
        }
    }
}
