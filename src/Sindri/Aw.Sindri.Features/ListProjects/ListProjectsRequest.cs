using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListProjects;

/// <summary>The Projects page (GET /sindri/projects[?all=true]; projects page spec §6.2).</summary>
/// <param name="All">Every finished project, not just the last 14 days.</param>
public sealed record ListProjectsRequest(bool All) : IQuery<ListProjectsResponse>;
