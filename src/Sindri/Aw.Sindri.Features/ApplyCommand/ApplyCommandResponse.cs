using System.Text.Json.Serialization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>What a command did (engine spec §5.2, §6.3): the instance after it and what happened, the nonce it used, and whether that nonce had already applied this same command.</summary>
/// <param name="Result">The instance after the command, and what happened ('resumed (transition): -> produce').</param>
/// <param name="Nonce">The nonce the command used (printed with '--json').</param>
/// <param name="AlreadyApplied">Whether the same command was already applied with this nonce, so nothing new happened.</param>
public sealed record ApplyCommandResponse(CommandResult Result, CommandNonce Nonce, bool AlreadyApplied)
{
    /// <summary>
    /// For a takeover of a Step with a live session (issue 1004): what opening it in a viewer came to, as the launch's viewer record (outcome opened, unconfirmed, failed, or
    /// skipped when no viewer is usable; 'reason' says why when nothing opened). Null for every other command, for a takeover with no live session, and for a repeat of one that already applied.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LaunchViewer? Viewer { get; init; }

    /// <summary>The command that attaches to the opened session, with <see cref="Viewer"/>; null when it is.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Attach { get; init; }
}
