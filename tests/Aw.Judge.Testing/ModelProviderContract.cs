using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The IModelProvider contract: Decide answers every outcome its backend can meet with a result (decided, errored or
/// unavailable) and throws only OperationCanceledException for the caller's token. <see cref="ScriptedProvider"/>, the
/// rules provider, the three agent CLIs and Jev pass it. A provider that can't reach a case returns null from its hook,
/// and the case holds trivially for it.
/// </summary>
public abstract class ModelProviderContract
{
    /// <summary>The question every case asks: three outputs over message-meta text.</summary>
    protected static QuestionRef Question { get; } = new()
    {
        Name = "wake-gate",
        Outputs = ["send", "batch", "drop"],
        Prompt = "Classify the message.",
        ContentClass = ContentClass.MessageMeta,
    };

    /// <summary>The input every case sends.</summary>
    protected static JsonNode Input => new JsonObject { ["text"] = "hello", ["senderKind"] = "main" };

    /// <summary>The budget every case gives.</summary>
    protected static TimeSpan Budget { get; } = TimeSpan.FromSeconds(2);

    /// <summary>The provider as installed (for its classes and the cancellation case).</summary>
    /// <returns>The provider.</returns>
    protected abstract IModelProvider Create();

    /// <summary>A provider whose model answers <paramref name="decision"/>, or null when it never asks a model.</summary>
    /// <param name="decision">The decision the model gives.</param>
    /// <returns>The provider, or null.</returns>
    protected abstract IModelProvider? Answering(string decision);

    /// <summary>A provider whose backend fails (a non-zero exit, a server error), or null when it has none.</summary>
    /// <returns>The provider, or null.</returns>
    protected abstract IModelProvider? Failing();

    /// <summary>A provider whose backend runs past any budget, or null when it can't.</summary>
    /// <returns>The provider, or null.</returns>
    protected abstract IModelProvider? Hanging();

    /// <summary>It covers at least one class and reports the same set each time.</summary>
    [Fact]
    public void It_covers_at_least_one_class_and_says_the_same_each_time()
    {
        var provider = Create();

        Expect.That(provider.Classes.Count > 0, "a provider covers at least one content class");
        Expect.That(ReferenceEquals(provider.Classes, provider.Classes), "the classes are one stable set");
    }

    /// <summary>The caller's cancellation is the one exception Decide throws.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_callers_cancellation_throws_OperationCanceledException()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        ProviderResult? result = null;
        var threw = false;
        try
        {
            result = await Create().Decide(Question, Input, Budget, cancelled.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Expect.That(threw, $"a cancelled call throws OperationCanceledException, got {result}");
    }

    /// <summary>An answer within the outputs is decided with that decision.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_answer_within_the_outputs_is_decided()
    {
        if (Answering("batch") is not { } provider)
        {
            return;   // it never asks a model
        }

        var result = await provider.Decide(Question, Input, Budget, CancellationToken.None);

        Expect.That(result is ProviderDecided decided && string.Equals(decided.Decision, "batch", StringComparison.Ordinal), $"decided 'batch', got {result}");
    }

    /// <summary>An answer outside the outputs is errored 'out-of-enum', never decided.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_answer_outside_the_outputs_is_errored_out_of_enum()
    {
        if (Answering("wake-everyone") is not { } provider)
        {
            return;   // it never asks a model
        }

        var result = await provider.Decide(Question, Input, Budget, CancellationToken.None);

        Expect.That(result == new ProviderErrored("out-of-enum"), $"errored 'out-of-enum', got {result}");
    }

    /// <summary>A failing backend is a result, not a throw.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_failing_backend_is_errored_or_unavailable_not_thrown()
    {
        var provider = Failing() ?? Create();

        var result = await provider.Decide(Question, Input, Budget, CancellationToken.None);

        Expect.That(result is ProviderErrored or ProviderUnavailable, $"errored or unavailable, got {result}");
    }

    /// <summary>Past the budget it stops and is unavailable with 'timeout'.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Past_the_budget_it_is_unavailable_with_timeout()
    {
        if (Hanging() is not { } provider)
        {
            return;   // it can't run past a budget
        }

        var result = await provider.Decide(Question, Input, TimeSpan.FromMilliseconds(200), CancellationToken.None);

        Expect.That(result == new ProviderUnavailable(ProviderUnavailable.Timeout), $"unavailable 'timeout', got {result}");
    }
}
