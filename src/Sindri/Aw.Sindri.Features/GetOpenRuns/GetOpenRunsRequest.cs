using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetOpenRuns;

/// <summary>What would stop a restart (GET /sindri/runs/open, first-run spec decision 11): launched agent sessions, workflow runs that are running or parked, and jobs that are running.</summary>
internal sealed record GetOpenRunsRequest() : IQuery<GetOpenRunsResponse>;
