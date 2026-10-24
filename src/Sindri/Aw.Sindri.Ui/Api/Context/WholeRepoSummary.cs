namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One repo the whole map covers.</summary>
/// <param name="Name">The repo's name.</param>
/// <param name="NodeCount">How many nodes it has on the map.</param>
internal sealed record WholeRepoSummary(string Name, int NodeCount);
