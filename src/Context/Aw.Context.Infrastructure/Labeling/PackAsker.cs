using Aw.Context.Core;
using Aw.Context.Core.Models;

namespace Aw.Context.Infrastructure.Labeling;

/// <summary>
/// Asks Jev a list of questions the way every pick does (model routing spec §6.3, §6.4): packs sized against its input cap, the pass's token budget checked before each pack, a refused pack split in halves down to
/// <see cref="LabelPickLimits.MaxSplitDepth"/>, and a provider that can't be asked ending the pass. One asker serves one pass: it keeps the answers and remembers why it stopped.
/// </summary>
/// <param name="port">Jev.</param>
/// <param name="kind">Which pick this is.</param>
internal sealed class PackAsker(IChoicePort port, ChoiceKind kind)
{
    private readonly Dictionary<string, ChoiceAnswer> _answers = new(StringComparer.Ordinal);

    /// <summary>The tokens the pass has used so far, the caller's start plus every pack asked.</summary>
    internal int Spent { get; set; }

    /// <summary>Why no more calls are made ('token-cap', or a port reason code), or null while asking can go on.</summary>
    internal string? Stopped { get; private set; }

    /// <summary>The answers collected, by question key.</summary>
    internal IReadOnlyDictionary<string, ChoiceAnswer> Answers => _answers;

    internal async ValueTask Ask(List<ChoiceQuestion> questions, int maxTokens, CancellationToken cancellationToken)
    {
        var cap = await port.InputCapChars(cancellationToken).ConfigureAwait(false);
        foreach (var pack in LabelPacks.Pick(questions, cap).Packs)
        {
            if (Stopped is not null)
            {
                return;
            }

            var estimate = Estimate(pack);
            if (Spent + estimate > maxTokens)
            {
                Stopped = "token-cap";
                return;
            }

            await Ask(pack, cap, depth: 0, cancellationToken).ConfigureAwait(false);
            Spent += estimate;
        }
    }

    // A refused pack splits in halves down to MaxSplitDepth; a provider that can't be asked at all stops every later call of the pass.
    private async ValueTask Ask(IReadOnlyList<ChoiceQuestion> pack, int cap, int depth, CancellationToken cancellationToken)
    {
        var reply = await port.Pick(kind, pack, cap, cancellationToken).ConfigureAwait(false);
        if (reply.IsSuccess)
        {
            foreach (var answer in reply.Value)
            {
                _answers[answer.Key] = answer;
            }

            return;
        }

        if (reply.Error.Code != ContextErrors.ChoiceRejected.Code)
        {
            Stopped = reply.Error.Message;
            return;
        }

        if (pack.Count >= 2 && depth < LabelPickLimits.MaxSplitDepth)
        {
            var half = pack.Count / 2;
            await Ask([.. pack.Take(half)], cap, depth + 1, cancellationToken).ConfigureAwait(false);
            if (Stopped is null)
            {
                await Ask([.. pack.Skip(half)], cap, depth + 1, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static int Estimate(IReadOnlyList<ChoiceQuestion> pack) => LabelGenerator.Tokens(new string('x', pack.Sum(LabelPacks.ChoiceChars))) + (pack.Count * LabelPickLimits.AnswerTokens);
}
