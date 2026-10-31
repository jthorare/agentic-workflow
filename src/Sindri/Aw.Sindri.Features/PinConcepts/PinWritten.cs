using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.PinConcepts;

/// <summary>One pin a PinConcepts job touched.</summary>
/// <param name="Id">The pin's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Action">'created', 'updated' or 'removed'.</param>
public sealed record PinWritten(ConceptPinId Id, string Name, string Action);
