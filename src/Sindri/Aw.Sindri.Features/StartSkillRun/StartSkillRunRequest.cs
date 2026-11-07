using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.StartSkillRun;

/// <summary>
/// 'sindri skill run &lt;id&gt;' (POST /sindri/skills/{id}/runs; skill flows spec §7): runs one skill block as a trial, a one-state recipe of its own that the owner's request approves. A launched session is refused
/// (desk class); the run is always scrubbed, so a block that pushes cannot push from it.
/// </summary>
/// <param name="Block">The block id.</param>
/// <param name="Item">The item the trial runs on (the CLI creates one on the repo of the cwd, or the one --repo names).</param>
/// <param name="Inputs">The input values by key.</param>
/// <param name="Skills">Null, or the skills folder to read this one block from instead of the loaded set (the author loop); the loaded set is unchanged.</param>
internal sealed record StartSkillRunRequest(string Block, string Item, IReadOnlyDictionary<string, string> Inputs, string? Skills = null) : ICommand<StartSkillRunResponse>;
