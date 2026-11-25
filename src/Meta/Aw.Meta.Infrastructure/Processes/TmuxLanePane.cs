using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>
/// ILanePane over tmux. Every target is the exact session ("=name:", so a name that is a prefix of another matches nothing else;
/// send-keys wants the colon to resolve the pane), and the wake line is typed in literal mode ("-l"), so it is never read as key names.
/// </summary>
/// <param name="runner">Runs tmux.</param>
/// <param name="tmux">The tmux program (AW_TMUX, default tmux).</param>
/// <param name="socket">The tmux socket name (AW_META_TMUX_SOCKET), passed as "-L"; null means the team's own socket, "-L" plus the team name, where the launcher starts every lane.</param>
/// <param name="config">The team config, read for the team name when no socket is set.</param>
/// <param name="delay">Waits between the wake line and its Enter.</param>
internal sealed class TmuxLanePane(ICommandRunner runner, string tmux, string? socket, IMetaConfig config, IDelay delay) : ILanePane
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _enterPause = TimeSpan.FromMilliseconds(300);

    public async ValueTask<Result<PaneState?>> Probe(string session, CancellationToken cancellationToken)
    {
        var name = await SocketName(cancellationToken).ConfigureAwait(false);
        if (name.IsError)
        {
            return Results.Success<PaneState?>(value: null);   // no team config: no socket to look on, the lane is down
        }

        var target = Target(session);
        var command = await Run(name.Value, ["display-message", "-p", "-t", target, "#{pane_current_command}"], cancellationToken).ConfigureAwait(false);
        if (command.IsError || command.Value.ExitCode != 0)
        {
            return Results.Success<PaneState?>(value: null);   // no tmux, or no such session: the lane is down
        }

        var clients = await Run(name.Value, ["list-clients", "-t", "=" + session, "-F", "x"], cancellationToken).ConfigureAwait(false);
        var count = clients.IsError || clients.Value.ExitCode != 0
            ? 0
            : clients.Value.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        return Results.Success<PaneState?>(new PaneState(command.Value.Stdout.Trim(), count));
    }

    public async ValueTask<Result<int?>> PanePid(string session, CancellationToken cancellationToken)
    {
        var name = await SocketName(cancellationToken).ConfigureAwait(false);
        if (name.IsError)
        {
            return Results.Success<int?>(value: null);
        }

        var shown = await Run(name.Value, ["display-message", "-p", "-t", Target(session), "#{pane_pid}"], cancellationToken).ConfigureAwait(false);
        return Results.Success<int?>(
            shown.IsSuccess && shown.Value.ExitCode == 0 && int.TryParse(shown.Value.Stdout.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pid) ? pid : null);
    }

    public async ValueTask<Result<Unit>> Type(string session, string text, CancellationToken cancellationToken) =>
        await Keys(session, ["send-keys", "-t", Target(session), "-l", text], cancellationToken).ConfigureAwait(false);

    public async ValueTask<Result<Unit>> PressEnter(string session, CancellationToken cancellationToken) =>
        await Keys(session, ["send-keys", "-t", Target(session), "Enter"], cancellationToken).ConfigureAwait(false);

    public async ValueTask<Result<Unit>> ClearInput(string session, CancellationToken cancellationToken) =>
        await Keys(session, ["send-keys", "-t", Target(session), "C-u"], cancellationToken).ConfigureAwait(false);

    public async ValueTask<Result<string>> Capture(string session, CancellationToken cancellationToken)
    {
        var name = await SocketName(cancellationToken).ConfigureAwait(false);
        if (name.IsError)
        {
            return name.Error;
        }

        var shown = await Run(name.Value, ["capture-pane", "-p", "-t", Target(session)], cancellationToken).ConfigureAwait(false);
        return shown.IsError
            ? shown.Error
            : shown.Value.ExitCode == 0 ? shown.Value.Stdout : MetaErrors.CommandFailed.With("tmux capture-pane exited " + shown.Value.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + shown.Value.Stderr.Trim());
    }

    private async ValueTask<Result<Unit>> Keys(string session, string[] arguments, CancellationToken cancellationToken)
    {
        var name = await SocketName(cancellationToken).ConfigureAwait(false);
        if (name.IsError)
        {
            return name.Error;
        }

        var ran = await Run(name.Value, arguments, cancellationToken).ConfigureAwait(false);
        return ran.IsError
            ? ran.Error
            : ran.Value.ExitCode == 0 ? Results.Ok() : MetaErrors.CommandFailed.With("tmux " + arguments[0] + " exited " + ran.Value.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + " for " + session + ": " + ran.Value.Stderr.Trim());
    }

    public async ValueTask<Result<Unit>> Wake(string session, string line, CancellationToken cancellationToken)
    {
        var name = await SocketName(cancellationToken).ConfigureAwait(false);
        if (name.IsError)
        {
            return name.Error;
        }

        var target = Target(session);
        var typed = await Run(name.Value, ["send-keys", "-t", target, "-l", line], cancellationToken).ConfigureAwait(false);
        if (typed.IsSuccess && typed.Value.ExitCode == 0)
        {
            // Codex's input box swallows an Enter that follows the typed characters at once (it reads the burst as a paste).
            await delay.Wait(_enterPause, cancellationToken).ConfigureAwait(false);
        }

        var entered = typed.IsError || typed.Value.ExitCode != 0 ? typed : await Run(name.Value, ["send-keys", "-t", target, "Enter"], cancellationToken).ConfigureAwait(false);
        return entered.IsError
            ? entered.Error
            : entered.Value.ExitCode == 0 ? Results.Ok() : MetaErrors.CommandFailed.With("tmux send-keys exited " + entered.Value.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + entered.Value.Stderr.Trim());
    }

    private static string Target(string session) => "=" + session + ":";

    private async ValueTask<Result<string>> SocketName(CancellationToken cancellationToken)
    {
        if (socket is not null)
        {
            return socket;
        }

        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        return team.IsError ? team.Error : team.Value.Team;
    }

    private ValueTask<Result<CommandResult>> Run(string socketName, string[] arguments, CancellationToken cancellationToken) =>
        runner.Run(new CommandRequest([tmux, "-L", socketName, .. arguments], _timeout), cancellationToken);
}
