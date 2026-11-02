namespace Aw.Judge.Core.Questions;

/// <summary>
/// The one rule for fitting several texts into a budget: cut the longest first. Every text is cut to the same length L
/// (a shorter text is never touched), and L is the largest length at which the whole still fits.
/// </summary>
public static class FairCut
{
    /// <summary>The largest per-text length at which <paramref name="fits"/> still holds; 0 when even that doesn't fit.</summary>
    /// <param name="longest">The length of the longest text, the most L can usefully be.</param>
    /// <param name="fits">Whether the whole fits when every text is cut to the given length; grows harder as the length grows.</param>
    /// <returns>The length.</returns>
    public static int Limit(int longest, Func<int, bool> fits)
    {
        ArgumentNullException.ThrowIfNull(fits);
        int low = 0, high = longest, best = 0;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            if (fits(mid))
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }
}
