using Aw.Kernel;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// What a session Sindri itself launched may not do (owner decision q100): answer a human step through a command that carries no launch secret.
/// An owner-started session (no launch), a human and a service are not touched. Pure.
/// </summary>
public static class LaunchedCallers
{
    /// <summary>The refusal (SND-AUTH-002) for a launched session answering a human step: the instance has an open desk task or question and no agent run.</summary>
    /// <param name="caller">Who calls.</param>
    /// <param name="view">The instance the command acts on, as read.</param>
    /// <param name="action">What the caller tried, for the message ('workflow answer', 'workflow command approve').</param>
    /// <returns>The refusal; null when the call may go on.</returns>
    public static Error? RefuseOnHumanStep(CallerContext caller, InstanceView view, string action)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(view);
        if (caller.Launch is null || view.AgentRun is not null || (view.Task is null && view.Question is null))
        {
            return null;
        }

        var step = view.Task?.Step.Value ?? view.State;
        return SindriErrors.AuthRefused.With(
            $"{action} on '{step}' is a human step: a session Sindri launched can't answer it. Ask the human, or an agent session the owner started");
    }
}
