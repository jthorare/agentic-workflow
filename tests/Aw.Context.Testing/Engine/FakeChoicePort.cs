using Aw.Context.Core;
using Aw.Context.Core.Models;
using Aw.Kernel;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The in-memory <see cref="IChoicePort"/>: answers each pack with <see cref="Script"/> (by default it picks the last option of every question at confidence 0.9), records every pack it was asked, and reports <see cref="Identity"/>
/// and <see cref="Cap"/>. It passes <see cref="ChoicePortContract{TPort}"/>.
/// </summary>
public sealed class FakeChoicePort : IChoicePort
{
    /// <summary>What the pretend Jev answers to a pack; null answers every question with its last option.</summary>
    public Func<IReadOnlyList<ChoiceQuestion>, Result<IReadOnlyList<ChoiceAnswer>>>? Script { get; set; }

    /// <summary>The cap <see cref="InputCapChars"/> reports (the judge's default is 160,000).</summary>
    public int Cap { get; set; } = 160_000;

    /// <summary>The model <see cref="Identity"/> reports.</summary>
    public ChoiceModel Model { get; set; } = new("jev-test-1");

    /// <summary>The reason <see cref="Unavailable"/> reports (<see cref="ChoiceReasons.Off"/> or <see cref="ChoiceReasons.NoKey"/>); null, the default, means a pick can go out.</summary>
    public string? UnavailableReason { get; set; }

    /// <summary>The packs asked so far, failed ones included, with the kind and the cap each was sized for.</summary>
    public IList<(ChoiceKind Kind, IReadOnlyList<ChoiceQuestion> Questions, int CapChars)> Asked { get; } = [];

    /// <inheritdoc />
    public ChoiceModel Identity => Model;

    /// <inheritdoc />
    public ValueTask<int> InputCapChars(CancellationToken cancellationToken) => ValueTask.FromResult(Cap);

    /// <inheritdoc />
    public ValueTask<string?> Unavailable(ChoiceKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(UnavailableReason);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<ChoiceAnswer>>> Pick(ChoiceKind kind, IReadOnlyList<ChoiceQuestion> questions, int capChars, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(questions);
        cancellationToken.ThrowIfCancellationRequested();
        Asked.Add((kind, questions, capChars));
        return ValueTask.FromResult(Script?.Invoke(questions) ?? Results.Success<IReadOnlyList<ChoiceAnswer>>([.. questions.Select(q => new ChoiceAnswer(q.Key, q.Options[^1], 0.9))]));
    }

    /// <summary>A script that fails every pack with one of the port's errors.</summary>
    /// <param name="rejected">True for <see cref="ContextErrors.ChoiceRejected"/> (the labeler may split), false for <see cref="ContextErrors.ChoiceUnavailable"/> (it stops asking).</param>
    /// <param name="reason">The reason code, as the error's message.</param>
    /// <returns>The script.</returns>
    public static Func<IReadOnlyList<ChoiceQuestion>, Result<IReadOnlyList<ChoiceAnswer>>> Failing(bool rejected, string reason) =>
        _ => rejected ? ContextErrors.ChoiceRejected.With(reason) : ContextErrors.ChoiceUnavailable.With(reason);
}
