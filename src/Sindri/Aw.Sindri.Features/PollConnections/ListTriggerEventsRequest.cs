using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>List the events of a kind seen on this box (GET /sindri/workflows/trigger-events, spec 2026-11-12-sindri-workflows-page-design.md §4.3 and §9 E6).</summary>
/// <param name="Kind">The catalog id ('github.pr.merged'); null or unknown is invalid input.</param>
/// <param name="Connection">One connection ('github:work'), or null for every one.</param>
/// <param name="Filter">A trigger filter ('trigger.repo == "acme/api"') each event is checked against, or null.</param>
/// <param name="Limit">How many events, at most; <see cref="ListTriggerEventsService.DefaultLimit"/> when null.</param>
internal sealed record ListTriggerEventsRequest(string? Kind, string? Connection = null, string? Filter = null, int? Limit = null) : IQuery<ListTriggerEventsResponse>;
