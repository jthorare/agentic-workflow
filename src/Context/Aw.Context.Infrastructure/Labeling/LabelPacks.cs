using Aw.Context.Core.Models;

#pragma warning disable MA0182 // the stub until the commit that uses it
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
    internal static PackPlan<T> First<T>(IEnumerable<T> items, Func<T, int> chars, int budgetChars, int maxItems, int fixedChars) => throw new NotSupportedException();

    /// <summary>Packs choice questions against Jev's input cap: all the characters of one call (every question and option text) within <see cref="LabelPickLimits.CapShare"/> of it, at most <see cref="LabelPickLimits.MaxQuestions"/> questions.</summary>
    /// <param name="questions">The questions, in the order the packs must keep.</param>
    /// <param name="capChars">Jev's input cap in characters.</param>
    /// <returns>The packs; a cap too small for even one question leaves them all in <see cref="PackPlan{T}.Unfit"/>.</returns>
    internal static PackPlan<ChoiceQuestion> Pick(IReadOnlyList<ChoiceQuestion> questions, int capChars) => throw new NotSupportedException();
}
