using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CreateToolkit;

/// <summary>Makes a new toolkit as a job (agent-first PR 16c): the generator writes a toolkit project with one sample tool under the toolkits root, and a toolkit row in state 'off' records it. It still needs a build.</summary>
/// <param name="Name">The toolkit name: lowercase words joined by '-', each starting with a letter (github-triage).</param>
/// <param name="Tool">The first tool's name, snake_case (list_issues).</param>
/// <param name="Needs">What the toolkit needs, as the generator's --needs list (database:state,disk:1g,connection:github), or null for nothing.</param>
internal sealed record CreateToolkitRequest(string Name, string Tool, string? Needs = null) : IJob<CreateToolkitResult>;
