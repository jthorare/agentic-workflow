using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'session.usage' payload (API spec §3.4, SCR E-28): one usage sample of a launched session. Not public. Key 'session.usage:&lt;launch&gt;:&lt;n&gt;'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Sample">The sample's number in this launch, from 1.</param>
/// <param name="InputTokens">Input tokens so far.</param>
/// <param name="OutputTokens">Output tokens so far.</param>
/// <param name="Target">The agent the launch runs.</param>
/// <param name="Model">The model the tokens are priced as: the one with the most tokens when a session used several, or null when the reader named none.</param>
public sealed record SessionUsage([property: JsonPropertyName("launchId")] LaunchId Launch, int Sample, long InputTokens, long OutputTokens, AgentId Target, string? Model = null);
