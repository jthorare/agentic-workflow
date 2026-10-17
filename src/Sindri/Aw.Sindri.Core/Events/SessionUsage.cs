using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'session.usage' payload (SCR E-28): one usage sample of a launched session. Not public. Key 'session.usage:&lt;launch&gt;:&lt;n&gt;'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Sample">The sample's number in this launch, from 1.</param>
/// <param name="InputTokens">Input tokens so far.</param>
/// <param name="OutputTokens">Output tokens so far.</param>
public sealed record SessionUsage(LaunchId Launch, int Sample, long InputTokens, long OutputTokens);
