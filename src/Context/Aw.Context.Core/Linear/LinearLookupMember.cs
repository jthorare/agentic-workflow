using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>A member of a team, for the assignee picker.</summary>
/// <param name="Id">The user's Linear id.</param>
/// <param name="Name">Their name.</param>
/// <param name="DisplayName">Their short name, or null.</param>
/// <param name="Active">False for a member who left; the pickers skip them, an issue they hold still names them.</param>
public sealed record LinearLookupMember(ExternalId Id, string Name, string? DisplayName, bool Active);
