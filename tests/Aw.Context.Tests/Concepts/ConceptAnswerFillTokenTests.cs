using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Context.Testing.Concepts;
using Aw.Context.Tests.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.Concepts;

/// <summary>Issue 4176: the host's stop waits for a running fill until the fill sees its token, so every store call the fill makes must get that token, or a slow store holds the stop.</summary>
[Trait("Lane", "unit")]
public sealed class ConceptAnswerFillTokenTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task A_fill_hands_its_token_to_every_store_call()
    {
        _ = await _world.ImportFixture();
        var store = new TokenRecordingStore(new FakeConceptAnswerStore(_world.Store, _world.Intents));   // why: a fresh store keeps nothing yet, so the fill reads and keeps every answer
        var answers = new ConceptAnswers(_world.Service<ConceptReader>(), store, _world.Service<IScrubberFactory>());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await answers.Fill(MapWorld.HumanVia(ContextClient.Http), stop.Token);

        Assert.Contains(store.Calls, c => string.Equals(c.Call, "keep-concepts", StringComparison.Ordinal));
        Assert.Contains(store.Calls, c => string.Equals(c.Call, "keep-glossary", StringComparison.Ordinal));
        Assert.All(store.Calls, c => Assert.True(c.Token == stop.Token, $"the store's {c.Call} call got another token than the fill's"));
    }

    /// <summary>Records the token each call got, then asks the inner store.</summary>
    private sealed class TokenRecordingStore(IConceptAnswerStore inner) : IConceptAnswerStore
    {
        public List<(string Call, CancellationToken Token)> Calls { get; } = [];

        public ValueTask<Result<ConceptAnswerBasis?>> Basis(MapRead read, CancellationToken cancellationToken) =>
            inner.Basis(read, Record("basis", cancellationToken));

        public ValueTask<Result<KeptConcepts?>> Concepts(string key, CancellationToken cancellationToken) =>
            inner.Concepts(key, Record("concepts", cancellationToken));

        public ValueTask<Result<ConceptGlossaryView?>> Glossary(string key, CancellationToken cancellationToken) =>
            inner.Glossary(key, Record("glossary", cancellationToken));

        public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, KeptConcepts view, CancellationToken cancellationToken) =>
            inner.Keep(key, basis, view, Record("keep-concepts", cancellationToken));

        public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, ConceptGlossaryView view, CancellationToken cancellationToken) =>
            inner.Keep(key, basis, view, Record("keep-glossary", cancellationToken));

        private CancellationToken Record(string call, CancellationToken token)
        {
            Calls.Add((call, token));
            return token;
        }
    }
}
