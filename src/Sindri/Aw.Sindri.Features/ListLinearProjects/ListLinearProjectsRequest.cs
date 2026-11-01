using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ListLinearProjects;

/// <summary>The Add project sheet's From Linear tab (GET /sindri/projects/linear; projects page spec §3.2, §6.2): the stored Linear projects the owner could still attach.</summary>
public sealed record ListLinearProjectsRequest : IQuery<ListLinearProjectsResponse>;
