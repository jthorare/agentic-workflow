using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetLastStart;

/// <summary>The last one-off start's repo and agent (GET /sindri/sessions/last-start; agents spec §15.4): what the New agent form offers first. A read, open to agent sessions.</summary>
public sealed record GetLastStartRequest() : IQuery<LastStartView>;
