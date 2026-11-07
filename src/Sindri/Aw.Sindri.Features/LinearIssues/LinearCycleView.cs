namespace Aw.Sindri.Features.LinearIssues;

/// <summary>An issue's cycle. The name is Linear text: fenced for a session.</summary>
/// <param name="Id">The cycle's Linear id.</param>
/// <param name="Number">The cycle's number.</param>
/// <param name="Name">The cycle's name, or 'Cycle &lt;number&gt;' when Linear gave none.</param>
internal sealed record LinearCycleView(string Id, int Number, string Name);
