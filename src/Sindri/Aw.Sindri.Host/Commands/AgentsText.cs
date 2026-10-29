using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.ListSessions;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri agents' and 'sindri agents show' as text (collector spec CI-8): a state is a word, never a colour.</summary>
internal static class AgentsText
{
    private const double Gib = 1024d * 1024 * 1024;
    private const double Mib = 1024d * 1024;

    /// <summary>The list: a head, one line per session, the recently ended ones, then one line per problem.</summary>
    /// <param name="list">The answer.</param>
    /// <returns>The text.</returns>
    internal static string Render(SessionList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }
        else
        {
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{list.Counts.Live} live · {list.Counts.Asking} asking · {list.Counts.OutsideWorkflow} outside a workflow"));
            foreach (var row in list.Sessions)
            {
                _ = text.AppendLine(Line(row));
            }
        }

        foreach (var ended in list.RecentlyEnded)
        {
            var resume = ended.Resume is { Count: > 0 } argv ? "  resume: " + string.Join(' ', argv) : string.Empty;
            _ = text.AppendLine($"ended  {ended.Session.Ref}  {Time(ended.EndedAt)}{resume}");
        }

        foreach (var problem in list.Problems)
        {
            _ = text.AppendLine($"problem {problem.Code} ({problem.Source}): {problem.Text}");
        }

        return text.ToString();
    }

    /// <summary>One session in full.</summary>
    /// <param name="view">The answer.</param>
    /// <returns>The text.</returns>
    internal static string Render(SessionView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var text = new StringBuilder();
        _ = text.AppendLine($"{view.Session.Ref}  {view.Session.ProviderLabel}");
        Fields(text, view);
        foreach (var entry in view.StateHistory ?? [])
        {
            _ = text.AppendLine($"  {Time(entry.At)}  {entry.State.ToString().ToLowerInvariant()}{(entry.Reason is { } reason ? " · " + reason.ToString().ToLowerInvariant() : string.Empty)}");
        }

        return text.ToString();
    }

    private static void Fields(StringBuilder text, SessionView view)
    {
        var row = view.Session;
        Field(text, "state", State(row) + (row.Estimated ? " (estimated)" : string.Empty) + " since " + Time(row.Since) + " from " + row.StateSource.ToString().ToLowerInvariant());
        Field(text, "repo", row.Repo?.Value);
        Field(text, "worktree", row.Worktree);
        Field(text, "host", row.Host.Label + (row.Host.SocketKind is { } kind ? $" (tmux socket: {kind})" : string.Empty));
        Field(text, "tmux", row.Host.Session is { } session ? $"{session} {row.Host.Pane}" : null);
        Field(text, "lane", row.Lane is { } lane ? $"{lane.Team}/{lane.Lane}" : null);
        Field(text, "launch", row.LaunchId?.Value);
        Field(text, "started", row.StartedAt is { } started ? Time(started) : null);
        Field(text, "title", row.Title);
        Field(text, "pid", row.Pid?.ToString(CultureInfo.InvariantCulture));
        Field(text, "cwd", view.Cwd);
        Field(text, "socket", row.Host.Socket);
        Field(text, "sandbox", Sandbox(row.Sandbox));
        Field(text, "writes to", row.Sandbox?.Folder);
    }

    private static string Line(SessionRow row) =>
        $"{row.Ref}  {State(row)}  {row.Repo?.Value ?? "-"}  {row.Host.Label}{(row.Estimated ? "  (estimated)" : string.Empty)}{(row.Title is { } title ? "  " + title : string.Empty)}{(Sandbox(row.Sandbox) is { } sandbox ? "  " + sandbox : string.Empty)}";

    // Spec §8 Surfaces: "sandboxed · mem 2.1/12 GiB", "sandboxed · attended · mem 2.1/12 GiB"; a plan that doesn't read says so, a scope that is gone has no memory to show; below 0.1 GiB used memory reads in MiB ("mem 2.4 MiB/6.3 GiB").
    private static string? Sandbox(SessionSandbox? sandbox)
    {
        if (sandbox is null)
        {
            return null;
        }

        var parts = new List<string> { "sandboxed" };
        if (sandbox.Attended)
        {
            parts.Add("attended");
        }

        if (sandbox.State == SessionSandboxState.Unavailable)
        {
            parts.Add("details unavailable");
        }
        else if (sandbox.Memory is { UsedBytes: { } used, MaxBytes: { } max })
        {
            parts.Add(used < Gib / 10
                ? string.Create(CultureInfo.InvariantCulture, $"mem {used / Mib:0.0} MiB/{max / Gib:0.#} GiB")
                : string.Create(CultureInfo.InvariantCulture, $"mem {used / Gib:0.0}/{max / Gib:0.#} GiB"));
        }

        return string.Join(" · ", parts);
    }

    private static string State(SessionRow row) =>
        row.State == SessionState.Asking && row.Asking is { } asking
            ? "ASKING · " + asking.Reason.ToString().ToLowerInvariant() + (asking.Tool is { } tool ? " · " + tool : string.Empty)
            : row.State.ToString().ToUpperInvariant();

    private static void Field(StringBuilder text, string name, string? value)
    {
        if (value is not null)
        {
            _ = text.AppendLine($"{name + ":",-10}{value}");
        }
    }

    private static string Time(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
