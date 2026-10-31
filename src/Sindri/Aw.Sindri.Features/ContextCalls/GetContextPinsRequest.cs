using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The pins of the live profile (GET /sindri/context/pins, concept pins spec §3.6 to §3.8).</summary>
/// <param name="Repo">A human caller's repo when the profile has more than one, or null: the profile's pins and that repo's own. A session gets its own repo's whatever it names.</param>
internal sealed record GetContextPinsRequest(string? Repo = null) : IQuery<ContextPinsView>;
