namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>How far a full re-read of the Linear feed has got.</summary>
/// <param name="Done">Issues read so far.</param>
/// <param name="Total">Issues to read; null when Sindri can't tell (it knows only how many it has stored).</param>
internal sealed record LinearReread(int Done, int? Total);
