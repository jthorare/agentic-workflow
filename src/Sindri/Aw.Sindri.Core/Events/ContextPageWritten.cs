using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'context.page.written' payload (pages spec §3.1): a SetPages job wrote pages into the live profile's 'index.pages'. Key 'context.page.written:&lt;hash&gt;'.</summary>
/// <param name="CommandId">The id of the service run that recorded it.</param>
/// <param name="Actor">Who wrote the pages: 'human:ui', 'human:cli', 'session:mcp' and the like (the caller kind and client).</param>
/// <param name="Launch">The launch of the session that wrote them, or null when a human or a service did.</param>
/// <param name="Hash">The live profile's hash after the write.</param>
/// <param name="Pages">Each page touched as '&lt;id&gt;:&lt;created|updated|removed&gt;', in write order.</param>
public sealed record ContextPageWritten(CommandId CommandId, string Actor, LaunchId? Launch, ProfileHash Hash, IReadOnlyList<string> Pages);
