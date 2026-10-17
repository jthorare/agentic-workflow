using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// The steps of `onyx up` in the one order they run, checked when the service is composed: exactly fetch, config, pull, start. The service chains the stamps in this order: each step is given the stamp of the one before it.
internal static class OnyxUpPlan
{
    internal static IReadOnlyList<IOnyxUpStep> Validate(IEnumerable<IOnyxUpStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        List<IOnyxUpStep> list = [.. steps];
        if (!list.Select(static s => s.Name).SequenceEqual(OnyxSteps.Order, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"the steps of `onyx up` must be {string.Join(", ", OnyxSteps.Order)} in that order, not {string.Join(", ", list.Select(static s => s.Name))}");
        }

        return list;
    }

    /// <summary>Gives a step the stamp of the step before it.</summary>
    /// <param name="step">The asking step's name.</param>
    /// <param name="upstream">What the plan passed.</param>
    /// <returns>The upstream stamp.</returns>
    /// <exception cref="InvalidOperationException">There is none: the step ran first or outside the plan (a broken invariant, since composition checks the order).</exception>
    internal static string Upstream(string step, string? upstream)
        => upstream ?? throw new InvalidOperationException($"the {step} step needs the stamp of the step before it; the steps of `onyx up` run out of order");
}
