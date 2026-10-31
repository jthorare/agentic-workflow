using Aw.Kernel;

namespace Aw.Context.Core.Models;

/// <summary>
/// Asks Jev to pick one option for each of several questions in one call (spec §6.7). The cluster label pick uses it, and so does the product-surface pick (R-8): one port, no second. The Host
/// implements it over Judge's batch classifier. Context stays free of Judge, and nothing but already-scrubbed strings is passed in.
/// </summary>
public interface IChoicePort
{
    /// <summary>The model that answers, as the cache key of every answer needs it before a question is asked.</summary>
    public ChoiceModel Identity { get; }

    /// <summary>The cap on what one call may send, in characters ('providers.inputCapChars' of the judge's config): the labeler packs against it.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The cap.</returns>
    public ValueTask<int> InputCapChars(CancellationToken cancellationToken);

    /// <summary>Whether a pick call of this kind can go out now: a cheap read of the approved settings and the key that makes no model call. The labeler asks it before it writes candidates nobody can pick from, and the label cache keys on it.</summary>
    /// <param name="kind">Which pick this is.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Null when a call can go out, otherwise the reason code why not: <see cref="ChoiceReasons.Off"/> (the kind's pick is not 'jev' in the approved profile) or <see cref="ChoiceReasons.NoKey"/> (no Jev key).</returns>
    public ValueTask<string?> Unavailable(ChoiceKind kind, CancellationToken cancellationToken);

    /// <summary>Asks one pack.</summary>
    /// <param name="kind">Which pick this is: it decides which profile key must say 'jev' and which event is written.</param>
    /// <param name="questions">The questions, 2 to 5 options each.</param>
    /// <param name="capChars">The cap the pack was sized for; a pack over <see cref="LabelPickLimits.CapShare"/> of it is refused.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The answers (a question the provider left out or answered in an unusable shape is absent), or <see cref="ContextErrors.ChoiceRejected"/> when the provider refused this pack (too big, an answer that wasn't one, a timeout)
    /// so the caller may split it, or <see cref="ContextErrors.ChoiceUnavailable"/> when nothing in this pass would change by asking again (no key, offline, excluded, a refused key, rate limited, a server error, the pick off).
    /// The message of both is a reason code (<see cref="ChoiceReasons"/>).
    /// </returns>
    public ValueTask<Result<IReadOnlyList<ChoiceAnswer>>> Pick(ChoiceKind kind, IReadOnlyList<ChoiceQuestion> questions, int capChars, CancellationToken cancellationToken);
}
