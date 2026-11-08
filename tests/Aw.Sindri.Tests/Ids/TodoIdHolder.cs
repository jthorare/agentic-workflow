using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Ids;

/// <summary>Wraps a TodoId so the JSON tests can send a missing or null property.</summary>
public sealed record TodoIdHolder(TodoId Id);
