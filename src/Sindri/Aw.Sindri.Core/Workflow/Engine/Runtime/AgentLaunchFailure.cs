namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What the engine does with a <see cref="Launch.FailedLaunch"/> (engine P8 D8).</summary>
/// <param name="Kind">Park or fail the Step.</param>
/// <param name="Code">The error code the launcher gave.</param>
/// <param name="Message">The launcher's message, which the park's question or the failure's reason carries.</param>
public sealed record AgentLaunchFailure(AgentLaunchFailureKind Kind, string Code, string Message)
{
    /// <summary>The sandbox refusal's reason ('no-bwrap', 'canary' …), read inside the launch activity because an error's extensions don't cross Temporal; null for any other failure.</summary>
    public string? Reason { get; init; }

    /// <summary>The credential source a -017 refusal names ('owner-copy' or 'separate-login'), otherwise null.</summary>
    public string? Source { get; init; }

    /// <summary>The word a step failure carries ('launch', or 'limit' for -019), from <see cref="Sandbox.SandboxCode.FailureReason"/>; null for any other failure.</summary>
    public string? FailureReason { get; init; }

    /// <summary>The text of the park's question or of the 'step.failed' reason.</summary>
    public string Text => Kind == AgentLaunchFailureKind.Park ? $"{Code}: {Message}" : $"launch: {Code}: {Message}";
}
