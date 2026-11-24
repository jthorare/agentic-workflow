using Aw.Context.Core.History;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.GetHistory;

/// <summary>
/// What a history read starts from: the map the caller sees and the window. The live route reads the map with <see cref="MapReader"/>; a backtest arm hands the pipeline an
/// as-of map (an <see langword="AsOfMapBuilder"/> graph as a <see cref="VisibleMap"/>) and the sample's creation instant as 'before', so one cut serves both (S-H decision 12).
/// </summary>
/// <param name="Map">The map the caller sees.</param>
/// <param name="Window">The window.</param>
/// <param name="Draft">True when the draft is read.</param>
internal sealed record HistoryInputs(VisibleMap Map, HistoryWindow Window, bool Draft)
{
    /// <summary>The answer's scrubber for a refusal's choice labels (MapText.ChoiceLabel, #4196); null leaves them as stored.</summary>
    public Func<string, string>? ChoiceLabel { get; init; }
}
