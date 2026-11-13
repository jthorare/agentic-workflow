using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.StartSession;

/// <summary>
/// Starts a one-off session (agents spec §15.8): checks the act id and the prompt, runs the checks (<see cref="StartPreflight"/>), makes the worktree and the session (<see cref="StartLaunch"/>) and records
/// an 'agent.op' event keyed by the act id (<see cref="StartRecord"/>). Only a 2xx is stored: a refusal, or a failure after the worktree was made, removes the half-made session and worktree and leaves the act
/// id free. A replayed act id after a 2xx answers the first result and starts nothing.
/// </summary>
/// <param name="preflight">The checks before the start.</param>
/// <param name="launch">Makes the worktree and the session.</param>
/// <param name="record">Writes the event and the remembered choice.</param>
/// <param name="panes">The socket of the started session.</param>
/// <param name="book">The stored starts.</param>
/// <param name="callers">The caller: only the person may start an agent (agents spec §15.2).</param>
internal sealed partial class StartSessionService(StartPreflight preflight, StartLaunch launch, StartRecord record, ISessionPanes panes, IStartedSessions book, ICallerContextAccessor callers)
    : IService<StartSessionRequest, StartedSession>
{
    internal const int MaxPrompt = 8000;

    public async ValueTask<Result<StartedSession>> Handle(StartSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { Class: CallerClass.Human })
        {
            return SindriErrors.AuthRefused.With("starting an agent is for the person: use the app or 'sindri agents start' in a terminal");
        }

        if (!request.ActId.IsInitialized() || request.ActId.Value is not { } actId || !ActIdShape.IsMatch(actId))
        {
            return SindriErrors.InvalidInput.With("actId is 8 to 32 characters of a-z and 0-9");
        }

        if (request.Prompt is { Length: > MaxPrompt } prompt)
        {
            return SindriErrors.AgentPromptTooLong.With($"the prompt is {prompt.Length} characters; the most is {MaxPrompt}");
        }

        using var hold = await book.Enter(request.ActId, cancellationToken).ConfigureAwait(false);
        return book.InMemory(request.ActId) is { } first
            ? Answer(actId, first.Pane)   // a replay after a 2xx: the first result, nothing started
            : await Start(request, actId, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<StartedSession>> Start(StartSessionRequest request, string actId, CancellationToken cancellationToken)
    {
        if (record.Grant() is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var plan = await preflight.Check(request, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var launched = await launch.Run(request, plan.Value, actId, cancellationToken).ConfigureAwait(false);
        if (launched.IsError)
        {
            return await Replayed(launched.Error, actId, cancellationToken).ConfigureAwait(false);
        }

        book.Keep(request.ActId, launched.Value);
        await record.Write(request, actId, launched.Value, grant).ConfigureAwait(false);
        return Answer(actId, launched.Value.Pane);
    }

    /// <summary>A host failure: a name that already lives is a start that went through earlier (a replay after a restart), so it answers what was stored; any other failure is returned as it is.</summary>
    private async ValueTask<Result<StartedSession>> Replayed(Error error, string actId, CancellationToken cancellationToken) =>
        error.Kind == ErrorKind.Conflict && await book.Find(ActId.From(actId), cancellationToken).ConfigureAwait(false) is { } stored
            ? Answer(actId, stored.Pane)
            : error.Kind == ErrorKind.Conflict ? SindriErrors.AgentStartFailed.With(error.Message) : error;

    private StartedSession Answer(string actId, TmuxPaneId pane) =>
        new(SessionId.From("sindri-" + actId), ActId.From(actId), "starting", new StartedHost(panes.Socket, "sindri-" + actId, pane.Value));

    [GeneratedRegex("^[a-z0-9]{8,32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ActIdShape { get; }
}
