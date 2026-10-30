using Aw.Context.Core.Models;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="IChoicePort"/> honours, run against the fake and the Host's Jev adapter: the identity is the same string on every read, an answer is keyed by a question that was asked and picks only one of its
/// options, and a cancelled call throws.
/// </summary>
/// <typeparam name="TPort">The implementation.</typeparam>
public abstract class ChoicePortContract<TPort>
    where TPort : IChoicePort
{
    private static readonly ChoiceQuestion[] _questions =
    [
        new("c1", "Which name fits: src/tabs/TabManager.ts?", ["Tab groups", "Tabs"]),
        new("c2", "Which name fits: src/layout/SplitPane.ts?", ["Split panes", "Layout", "Panes"]),
    ];

    /// <summary>A fresh implementation that can answer every question.</summary>
    /// <returns>The port.</returns>
    protected abstract TPort Create();

    /// <summary>The identity is a non-empty string and the same on two reads.</summary>
    [Fact]
    public void Identity_is_one_stable_string()
    {
        var port = Create();

        Check(port.Identity.Value.Length > 0 && string.Equals(port.Identity.Value, port.Identity.Value, StringComparison.Ordinal), "the identity is a non-empty string, equal on two reads");
    }

    /// <summary>Every answer is for a question that was asked, and what it picked is one of that question's options.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Answers_are_for_asked_questions_and_pick_an_offered_option()
    {
        var port = Create();

        var answers = await port.Pick(ChoiceKind.Labeling, _questions, await port.InputCapChars(CancellationToken.None), CancellationToken.None);

        Check(answers.IsSuccess, "a small pack is answered");
        Check(answers.Value.All(a => _questions.Any(q => string.Equals(q.Key, a.Key, StringComparison.Ordinal) && (a.Picked is null || q.Options.Contains(a.Picked, StringComparer.Ordinal)))), "every answer is for a question asked and picks one of its options");
    }

    /// <summary>Asking whether a pick can go out is a read: it answers null or one of the port's reason codes, and null for a port that can answer a label pack.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Unavailable_is_null_for_a_port_that_can_answer()
    {
        var port = Create();

        Check(await port.Unavailable(ChoiceKind.Labeling, CancellationToken.None) is null, "a port that answers label packs says nothing is unavailable for them");
    }

    /// <summary>Whatever the kind, the reason is one of the two codes the labeler and the cache understand.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Unavailable_names_off_or_no_key_and_nothing_else()
    {
        var port = Create();

        foreach (var kind in new[] { ChoiceKind.Labeling, ChoiceKind.ProductSurfaces })
        {
            var reason = await port.Unavailable(kind, CancellationToken.None);

            Check(reason is null || ChoiceReasons.IsQuiet(reason), $"the reason for {kind} is null, 'off' or 'no-key'");
        }
    }

    /// <summary>A cancelled call throws OperationCanceledException, never an error result.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Cancellation_is_honoured()
    {
        var port = Create();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var thrown = await Record.ExceptionAsync(async () => await port.Pick(ChoiceKind.Labeling, _questions, 160_000, cancelled.Token));

        Check(thrown is OperationCanceledException, "a cancelled call throws OperationCanceledException");
    }

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TPort).Name}: {what}");
        }
    }
}
