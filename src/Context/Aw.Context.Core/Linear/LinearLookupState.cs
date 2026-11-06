using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>A workflow state of a team, for the state picker.</summary>
/// <param name="Id">Linear's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Type">triage, backlog, unstarted, started, completed or canceled.</param>
/// <param name="Position">Where Linear orders it among the team's states.</param>
public sealed record LinearLookupState(ExternalId Id, string Name, string Type, double Position);
