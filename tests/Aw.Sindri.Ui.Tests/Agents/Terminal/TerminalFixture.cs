using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>A terminal view on a fake clock, with targets for each kind of row (agents spec §15.3).</summary>
internal sealed class TerminalFixture
{
    internal TerminalFixture(bool isMac = false)
    {
        Control = new FakeSessionControl();
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 14, 0, 0, TimeSpan.Zero));
        View = new TerminalViewViewModel(Clock, isMac, Control);
        View.SocketRequested += (_, _) => Requests++;
        View.CloseRequested += (_, _) => Closes++;
        View.RemoveWorktreeRequested += (_, e) => RemovedWorktrees.Add(e.ActId);
        View.DiscardRequested += (_, e) => Discarded.Add(e.ActId);
    }

    internal FakeSessionControl Control { get; }

    internal FakeTimeProvider Clock { get; }

    internal TerminalViewViewModel View { get; }

    internal int Requests { get; private set; }

    internal int Closes { get; private set; }

    internal List<string> RemovedWorktrees { get; } = [];

    internal List<string> Discarded { get; } = [];

    internal static TerminalTarget Lane(string name = "ideas") =>
        new() { Key = "session:claude-code/" + name, Name = name, Kind = TerminalKind.Lane, SocketKind = "default", ResumeCommand = "claude --resume " + name };

    internal static TerminalTarget Listed(string name = "qa") =>
        new() { Key = "session:claude-code/" + name, Name = name, Kind = TerminalKind.Listed, SocketKind = "team" };

    internal static TerminalTarget OneOff(string actId = "abcd1234", string worktree = "") =>
        new() { Key = "sindri-" + actId, Name = "claude in sindri", Kind = TerminalKind.OneOff, SocketKind = "sindri", ActId = actId, WorktreePath = worktree, IsStarting = true, ResumeCommand = "claude --resume x" };

    internal static TerminalTarget Started(string name = "ideas") =>
        new() { Key = "session:claude-code/" + name, Name = name, Kind = TerminalKind.Listed, SocketKind = "sindri", StartedBySindri = true };

    internal static TerminalTarget Warp() =>
        new()
        {
            Key = "session:codex/w1",
            Name = "codex in web",
            Kind = TerminalKind.Listed,
            CanAttach = false,
            CantAttachReason = "Warp tab: Sindri can't show or type into this terminal.",
            TailText = "last line of the transcript",
            ResumeCommand = "codex resume w1",
        };

    /// <summary>Opens the target and brings the socket to Attached.</summary>
    internal TerminalFixture Attached(TerminalTarget target, TerminalOpenIntent intent = TerminalOpenIntent.Look)
    {
        View.Open(target, intent);
        View.SocketOpening();
        _ = View.Ready();
        View.FrameReceived(["a screen"], dead: false);
        if (target.IsStarting)
        {
            View.AgentReportedState();
        }

        return this;
    }
}
