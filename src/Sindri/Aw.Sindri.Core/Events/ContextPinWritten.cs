using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'context.pin.written' payload (concept pins spec §3.6): a PinConcepts job wrote pins into the live profile's 'index.concepts'. Key 'context.pin.written:&lt;hash&gt;'.</summary>
/// <param name="CommandId">The id of the service run that recorded it.</param>
/// <param name="Actor">Who wrote the pins: 'human:ui', 'human:cli', 'session:mcp' and the like (the caller kind and client).</param>
/// <param name="Launch">The launch of the session that wrote them, or null when a human or a service did.</param>
/// <param name="Hash">The live profile's hash after the write.</param>
/// <param name="Pins">Each pin touched as '&lt;id&gt;:&lt;created|updated|removed&gt;', in write order.</param>
public sealed record ContextPinWritten(CommandId CommandId, string Actor, LaunchId? Launch, ProfileHash Hash, IReadOnlyList<string> Pins);
