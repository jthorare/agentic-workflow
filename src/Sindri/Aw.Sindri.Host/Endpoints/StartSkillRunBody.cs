namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/skills/{id}/runs: the block comes from the route.</summary>
/// <param name="Item">The item the trial runs on.</param>
/// <param name="Inputs">The input values by key.</param>
/// <param name="Skills">Null, or the skills folder to read the block from.</param>
internal sealed record StartSkillRunBody(string Item, IReadOnlyDictionary<string, string>? Inputs = null, string? Skills = null);
