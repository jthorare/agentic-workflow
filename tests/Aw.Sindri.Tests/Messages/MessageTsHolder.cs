using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>A record that carries a ts, as a route response does, for the JSON tests.</summary>
/// <param name="Ts">The ts.</param>
/// <param name="Before">A ts that may be absent.</param>
public sealed record MessageTsHolder(MessageTs Ts, MessageTs? Before);
