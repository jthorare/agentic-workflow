using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The IBatchClassifier contract: the same tests run against the fake and the Jev adapter. A derived class supplies a
/// classifier whose Jev answers question 'q' with the choice 'yes' (the <see cref="Question"/> below), and the number of
/// requests that have reached that Jev.
/// </summary>
public abstract class BatchClassifierContract
{
    /// <summary>The question every contract batch asks.</summary>
    protected static readonly ChoiceBatchQuestion Question = new("q", "Is it so?", new Dictionary<string, string>(StringComparer.Ordinal) { ["yes"] = "it is", ["no"] = "it is not" });

    private static bool IsExcluded(ProviderResult result) => result is ProviderUnavailable unavailable && string.Equals(unavailable.ReasonCode, "excluded", StringComparison.Ordinal);

    private static readonly BatchRequest _batch = new(State: null, [Question], TimeSpan.FromSeconds(5));

    /// <summary>A classifier whose Jev answers <see cref="Question"/> with 'yes', and a count of the requests that reached that Jev.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The classifier and the count.</returns>
    protected abstract ValueTask<(IBatchClassifier Classifier, Func<int> Reached)> Create(CancellationToken cancellationToken);

    /// <summary>A batch that reaches Jev comes back decided, each answer under its question's key.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_batch_that_reaches_jev_is_answered_under_the_question_keys()
    {
        var (classifier, reached) = await Create(CancellationToken.None);

        var result = await classifier.Classify(_batch, providers: null, allowed: null, CancellationToken.None);

        var decided = result as BatchDecided;
        Expect.That(decided?.Answers.GetValueOrDefault("q") is ChoiceBatchAnswer answer && string.Equals(answer.Choice, "yes", StringComparison.Ordinal), "the answer comes back under the question's key");
        Expect.That(reached() == 1, "one request reached Jev");
    }

    /// <summary>An allowlist without jev leaves Jev unavailable, and nothing reaches it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_allowlist_without_jev_is_unavailable_and_asks_nothing()
    {
        var (classifier, reached) = await Create(CancellationToken.None);

        var result = await classifier.Classify(_batch, providers: null, allowed: [ProviderName.ClaudeCli], CancellationToken.None);

        Expect.That(IsExcluded(result), "an excluded Jev is unavailable, not an error");
        Expect.That(reached() == 0, "nothing reached Jev");
    }

    /// <summary>An allowlist with jev reaches it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_allowlist_with_jev_reaches_it()
    {
        var (classifier, reached) = await Create(CancellationToken.None);

        var result = await classifier.Classify(_batch, providers: null, allowed: [ProviderName.Jev], CancellationToken.None);

        Expect.That(result is BatchDecided, "an allowed Jev answers");
        Expect.That(reached() == 1, "one request reached Jev");
    }

    /// <summary>config.json's providers.jev: false leaves Jev unavailable, and nothing reaches it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Config_that_turns_jev_off_is_unavailable_and_asks_nothing()
    {
        var (classifier, reached) = await Create(CancellationToken.None);

        var result = await classifier.Classify(_batch, new ProvidersConfig(Jev: false), allowed: null, CancellationToken.None);

        Expect.That(IsExcluded(result), "a Jev the config turns off is unavailable, not an error");
        Expect.That(reached() == 0, "nothing reached Jev");
    }
}
