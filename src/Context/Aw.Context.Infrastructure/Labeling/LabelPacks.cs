using Aw.Context.Core.Models;

namespace Aw.Context.Infrastructure.Labeling;

/// <summary>Packs clusters and questions by their real string lengths (spec §6.2, §6.3), so a rerun builds the same packs.</summary>
internal static class LabelPacks
{
    /// <summary>Packs items in order: an item joins the open pack while the fixed text plus every item's characters stays within the budget and the pack is under the item limit.</summary>
    /// <typeparam name="T">What is packed.</typeparam>
    /// <param name="items">The items, in the order the packs must keep.</param>
    /// <param name="chars">The characters one item adds.</param>
    /// <param name="budgetChars">The characters a pack may hold.</param>
    /// <param name="maxItems">The most items in a pack.</param>
    /// <param name="fixedChars">The characters every pack carries besides its items (the instructions).</param>
    /// <returns>The packs and the items that fit none.</returns>
    internal static PackPlan<T> First<T>(IEnumerable<T> items, Func<T, int> chars, int budgetChars, int maxItems, int fixedChars)
    {
        var packs = new List<IReadOnlyList<T>>();
        var unfit = new List<T>();
        var open = new List<T>();
        var used = fixedChars;
        foreach (var item in items)
        {
            var size = chars(item);
            if (fixedChars + size > budgetChars)
            {
                unfit.Add(item);
                continue;
            }

            if (open.Count == maxItems || used + size > budgetChars)
            {
                packs.Add(open);
                (open, used) = ([], fixedChars);
            }

            open.Add(item);
            used += size;
        }

        if (open.Count > 0)
        {
            packs.Add(open);
        }

        return new PackPlan<T>(packs, unfit);
    }

    /// <summary>Packs choice questions against Jev's input cap: all the characters of one call (every question and option text) within <see cref="LabelPickLimits.CapShare"/> of it, at most <see cref="LabelPickLimits.MaxQuestions"/> questions.</summary>
    /// <param name="questions">The questions, in the order the packs must keep.</param>
    /// <param name="capChars">Jev's input cap in characters.</param>
    /// <returns>The packs; a cap too small for even one question leaves them all in <see cref="PackPlan{T}.Unfit"/>.</returns>
    internal static PackPlan<ChoiceQuestion> Pick(IReadOnlyList<ChoiceQuestion> questions, int capChars) =>
        First(questions, ChoiceChars, LabelPickLimits.CallChars(capChars), LabelPickLimits.MaxQuestions, fixedChars: 0);

    /// <summary>The characters a question adds to a call: its text and every option's.</summary>
    /// <param name="question">The question.</param>
    /// <returns>The characters.</returns>
    internal static int ChoiceChars(ChoiceQuestion question) => question.Prompt.Length + question.Options.Sum(static o => o.Length);
}
