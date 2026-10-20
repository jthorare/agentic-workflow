namespace Aw.Sindri.Core.Line;

/// <summary>One rule of a station: its id, the first stage it runs at, and its body.</summary>
/// <param name="Name">The rule id.</param>
/// <param name="From">The first stage it runs at (an evidence run runs all).</param>
/// <param name="Body">Reads the facts and reports into the sink.</param>
public sealed record LineRule(string Name, LineStage From, Action<LineFacts, LineSink> Body);
