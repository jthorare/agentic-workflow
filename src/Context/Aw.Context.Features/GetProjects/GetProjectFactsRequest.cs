using Aw.Context.Core.Engine;
using Aw.Context.Core.Projects;

namespace Aw.Context.Features.GetProjects;

/// <summary>Reads the stored projects the Projects page shows.</summary>
/// <param name="Request">Which projects were attached.</param>
/// <param name="Scope">The profile's settings and repos, and the caller.</param>
internal sealed record GetProjectFactsRequest(ProjectFactsRequest Request, ContextScope Scope);
