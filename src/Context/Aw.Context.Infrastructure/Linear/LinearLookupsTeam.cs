namespace Aw.Context.Infrastructure.Linear;

/// <summary>A team with the lists the pickers need.</summary>
/// <param name="Id">The Id Linear serves.</param>
/// <param name="Key">The Key Linear serves.</param>
/// <param name="Name">The Name Linear serves.</param>
/// <param name="States">The States Linear serves.</param>
/// <param name="Cycles">The Cycles Linear serves.</param>
/// <param name="Members">The Members Linear serves.</param>
/// <param name="Labels">The Labels Linear serves.</param>
internal sealed record LinearLookupsTeam(
    string Id,
    string Key,
    string? Name = null,
    LinearConnection<LinearLookupsState>? States = null,
    LinearConnection<LinearLookupsCycle>? Cycles = null,
    LinearConnection<LinearLookupsMember>? Members = null,
    LinearConnection<LinearNamed>? Labels = null);
