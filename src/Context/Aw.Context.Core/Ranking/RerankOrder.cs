using System.Text.Json;

namespace Aw.Context.Core.Ranking;

/// <summary>The rerank arm's answer (spec §8.2 step 6): a model's ordering of the top items, parsed defensively. Pure.</summary>
public static class RerankOrder
{
    /// <summary>The most items the model is asked to order.</summary>
    public const int DefaultTop = 30;

    /// <summary>Reads a JSON array of the numbers 1..<paramref name="asked"/>, best first, e.g. [3,1,2].</summary>
    /// <param name="answer">The model's text (untrusted).</param>
    /// <param name="asked">How many items were listed.</param>
    /// <returns>The zero-based positions, best first: numbers outside 1..asked and repeats are skipped and the items the model left out follow in their old order; null when the text is not a JSON array of numbers or names none of the items.</returns>
    public static IReadOnlyList<int>? Parse(string answer, int asked)
    {
        ArgumentNullException.ThrowIfNull(answer);
        var array = ArrayIn(answer);
        if (asked <= 0 || array is null)
        {
            return null;
        }

        var named = new List<int>();
        try
        {
            using var document = JsonDocument.Parse(array);
            foreach (var number in document.RootElement.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.Number))
            {
                if (number.TryGetInt32(out var n) && n >= 1 && n <= asked && !named.Contains(n - 1))
                {
                    named.Add(n - 1);
                }
            }
        }
        catch (JsonException)
        {
            return null;   // not an array of numbers: nothing was ordered
        }

        return named.Count == 0 ? null : [.. named, .. Enumerable.Range(0, asked).Where(i => !named.Contains(i))];
    }

    // The text from the first '[' to the last ']'; null when there is none, or when an object wraps it (not the asked-for shape).
    private static string? ArrayIn(string answer)
    {
        var (open, close, brace) = (answer.IndexOf('[', StringComparison.Ordinal), answer.LastIndexOf(']'), answer.IndexOf('{', StringComparison.Ordinal));
        return open < 0 || close <= open || (brace >= 0 && brace < open) ? null : answer[open..(close + 1)];
    }

    /// <summary>Puts the first items in the model's order and leaves the rest where they were.</summary>
    /// <param name="items">The items, best first.</param>
    /// <param name="top">How many were reranked.</param>
    /// <param name="order">The positions from <see cref="Parse"/>.</param>
    /// <returns>The reordered items.</returns>
    public static IReadOnlyList<FusedItem> Apply(IReadOnlyList<FusedItem> items, int top, IReadOnlyList<int> order)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(order);
        var head = Math.Min(top, items.Count);
        return order.Count != head || order.ToHashSet().Count != head || order.Any(i => i < 0 || i >= head)
            ? items
            : [.. order.Select(i => items[i]), .. items.Skip(head)];
    }
}
