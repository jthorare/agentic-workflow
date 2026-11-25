namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'AskingAgentNode' is asked (#3151): watch the launch's session for a permission prompt that differs from the one the workflow last saw. The activity reads the session's aw:fleet state file
/// every <paramref name="ReadEvery"/>, heartbeats on each read and returns as soon as the ask differs from <paramref name="Seen"/>, or after <paramref name="Cap"/> with the ask unchanged.
/// </summary>
/// <param name="Launch">The launch ('LCH-…').</param>
/// <param name="Seen">The ask the workflow last saw, or null for none.</param>
/// <param name="ReadEvery">How often the state file is read.</param>
/// <param name="Cap">How long one call may run before it hands back with nothing new, so one call never outlives the cap.</param>
internal sealed record AgentNodeAskWatch(string Launch, NodeSessionAsk? Seen, TimeSpan ReadEvery, TimeSpan Cap);
