using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>Which row kinds each <see cref="HomeSplit"/> holds (home spec §7.1 "Splits").</summary>
[SuppressMessage("Design", "MA0182", Justification = "Stub of the red commit (#3733, U1); the next commit uses it.")]
internal static class HomeSplits
{
    /// <summary>The tabs in header order.</summary>
    internal static IReadOnlyList<HomeSplit> InOrder { get; } = [HomeSplit.All, HomeSplit.Reviews, HomeSplit.Messages, HomeSplit.Work, HomeSplit.Agents];

    /// <summary>True when a row of this kind shows under the split.</summary>
    /// <param name="split">The split.</param>
    /// <param name="kind">The row's kind, such as 'review-requested'.</param>
    /// <returns>True when the split holds the kind.</returns>
    internal static bool Holds(HomeSplit split, string kind) => throw new NotSupportedException();
}
