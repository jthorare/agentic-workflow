using Aw.Context.Core.Models;
using Aw.Context.Core.Surfaces;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Labeling;

namespace Aw.Context.Infrastructure.Surfaces;

/// <summary>
/// Jev's decision on candidate groups (spec §6.9): one <see cref="ChoiceQuestion"/> per group, packed against Jev's input cap and split by halves as the label pick does (<see cref="PackAsker"/>), every group Jev did not settle left separate.
/// No CLI is asked in Jev's place.
/// </summary>
/// <param name="port">Jev, or null when the host has none.</param>
/// <param name="scrubbers">Builds the scrubbers every question passes through.</param>
internal sealed class SurfacePicker(IChoicePort? port, IScrubberFactory scrubbers) : ISurfacePicker
{
    public async ValueTask<SurfacePickResult> Decide(IReadOnlyList<SurfaceGroup> groups, SurfacePickOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Pick || port is null)
        {
            return Result(groups, new Dictionary<string, (ChoiceQuestion, ChoiceAnswer?)>(StringComparer.Ordinal), ChoiceReasons.Off);
        }

        // Nothing to build or ask when no call can go out: every group stays separate and the reason is told.
        if (await port.Unavailable(ChoiceKind.ProductSurfaces, cancellationToken).ConfigureAwait(false) is { } unavailable)
        {
            return Result(groups, new Dictionary<string, (ChoiceQuestion, ChoiceAnswer?)>(StringComparer.Ordinal), unavailable);
        }

        var built = scrubbers.For(options.ExtraPatterns);
        if (built.IsError)
        {
            return Result(groups, new Dictionary<string, (ChoiceQuestion, ChoiceAnswer?)>(StringComparer.Ordinal), built.Error.Code);
        }

        // Questions in key order, one per distinct key (a group built twice is asked once); a group that is not 1 to 3 candidates can't be asked and stays separate.
        var questions = groups.Where(static g => g.Candidates.Count is >= 1 and <= SurfaceGroup.MaxCandidates)
            .Select(g => SurfacePrompt.Question(g, built.Value.Egress))
            .DistinctBy(static q => q.Key, StringComparer.Ordinal)
            .OrderBy(static q => q.Key, StringComparer.Ordinal)
            .ToList();
        var asker = new PackAsker(port, ChoiceKind.ProductSurfaces) { Spent = options.SpentTokens };
        if (questions.Count > 0)
        {
            await asker.Ask(questions, options.MaxTokens, cancellationToken).ConfigureAwait(false);
        }

        return Result(groups, questions.ToDictionary(static q => q.Key, q => (q, asker.Answers.GetValueOrDefault(q.Key)), StringComparer.Ordinal), asker.Stopped);
    }

    private static SurfacePickResult Result(IReadOnlyList<SurfaceGroup> groups, Dictionary<string, (ChoiceQuestion Question, ChoiceAnswer? Answer)> asked, string? notDecided)
    {
        var verdicts = groups.Select(g => asked.TryGetValue(g.Key, out var a) && g.Candidates.Count is >= 1 and <= SurfaceGroup.MaxCandidates
            ? SurfacePrompt.Verdict(g, a.Question, a.Answer)
            : SurfaceVerdict.Separate(g.Key, decided: false)).ToList();
        var counts = new SurfaceCounts(
            verdicts.Count(static v => v.Choice == SurfaceChoice.Merge),
            verdicts.Count(static v => v.Choice == SurfaceChoice.Separate),
            verdicts.Count(static v => v.Choice == SurfaceChoice.Reject),
            verdicts.Count(static v => v is { Choice: SurfaceChoice.Separate, Decided: false }));
        return new SurfacePickResult(verdicts, counts, notDecided);
    }
}
