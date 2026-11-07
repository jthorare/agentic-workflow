using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>A label a team can put on an issue, for the label picker.</summary>
/// <param name="Id">Linear's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Color">Its colour as Linear spells it, '#eb5757'; empty when none was stored.</param>
public sealed record LinearLookupLabel(ExternalId Id, string Name, string Color);
