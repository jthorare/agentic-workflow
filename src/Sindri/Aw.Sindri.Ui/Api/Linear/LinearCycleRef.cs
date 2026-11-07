namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The cycle an issue sits in.</summary>
/// <param name="Id">The cycle's Linear id.</param>
/// <param name="Number">The cycle number.</param>
/// <param name="Name">The cycle's name.</param>
internal sealed record LinearCycleRef(string Id, int Number, string Name);
