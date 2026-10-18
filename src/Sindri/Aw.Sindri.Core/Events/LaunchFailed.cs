using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'launch.failed' payload (UI spec E-1, E-5): no session started. Key 'launch.outcome:&lt;launchId&gt;', shared with 'launch.started'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Code">The catalogue code (SND-LAUNCH-…, or the engine's SND-WF-… when it refused the takeover).</param>
/// <param name="Message">The error's message, with its fix.</param>
/// <param name="Reason">The refusal's reason for a sandbox code (-014 to -021: 'no-bwrap', 'expired', ...), so the engine and the app pick the sentence by (code, reason); left out otherwise.</param>
/// <param name="Source">The credential source for -017 ('owner-copy' or 'separate-login'); left out otherwise.</param>
/// <param name="Stage">Where the launch stopped ('host' or 'backstop'), or null for a failure before the launcher ran.</param>
public sealed record LaunchFailed(LaunchId Launch, ItemId Item, string Code, string Message, LaunchStage? Stage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Source = null);
