using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

/// <summary>Wraps a EventId so the JSON tests can send a missing or null property.</summary>
public sealed record EventIdHolder(EventId Id);
