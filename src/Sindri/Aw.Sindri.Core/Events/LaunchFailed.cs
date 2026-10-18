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
/// <param name="Detail">Where the launch stopped and the facts the error carried (launcher L-1, L-3), or null.</param>
public sealed record LaunchFailed([property: JsonPropertyName("launchId")] LaunchId Launch, ItemId Item, string Code, string Message, LaunchFailureDetail? Detail = null)
{
    /// <summary>Where the launch stopped ('host' or 'backstop'), or null for a failure before the launcher ran.</summary>
    [JsonIgnore]
    public LaunchStage? Stage => Detail?.Stage;

    /// <summary>The sandbox refusal's reason (-014 to -021), from <see cref="Detail"/>; null otherwise.</summary>
    [JsonIgnore]
    public string? Reason => Detail?.Reason;

    /// <summary>The credential source for -017 ('owner-copy' or 'separate-login'), from <see cref="Detail"/>; null otherwise.</summary>
    [JsonIgnore]
    public string? Source => Detail?.Source;
}
