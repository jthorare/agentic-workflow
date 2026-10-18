using System.Text.Json;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Host.Commands;

/// <summary>
/// Command name → entry. To add a command, write its static Entry and list it in <see cref="Entries"/>; parsing, the team-config
/// check and error output are done here.
/// </summary>
internal static class CommandTable
{
    private static readonly CommandEntry[] _entries =
    [
        RoleSetCommand.Entry,
        RoleShowCommand.Entry,
        TeamInitCommand.Entry,
        TeamShowCommand.Entry,
        TeamListCommand.Entry,
        VersionCommand.Entry,
        TeamMigrateCommand.Entry,
        SyncInboxCommand.Entry,
        StopCheckCommand.Entry,
        OutboxRecordCommand.Entry,
        InboxCommand.Entry,
        InboxDismissCommand.Entry,
        InboxDeferCommand.Entry,
        LanesRefreshCommand.Entry,
        LanesCommand.Entry,
        OwnerCommand.Entry,
        OwnerAnswerCommand.Entry,
        OwnerAskCommand.Entry,
        LogAppendCommand.Entry,
        StatusCommand.Entry,
        QueueDropCommand.Entry,
        GateRunCommand.Entry,
        QueueGateCommand.Entry,
        GateRerunCommand.Entry,
        MergeCheckCommand.Entry,
        MergeRunCommand.Entry,
        HealthCommand.Entry,
        WatchCommand.Entry,
        WorktreeListCommand.Entry,
        WorktreeSweepCommand.Entry,
        LaneSendCommand.Entry,
        LaneInboxCommand.Entry,
        LaneStopCheckCommand.Entry,
        LaneReplyCommand.Entry,
    ];

    // Commands that work across teams: they never need one selected.
    private static readonly HashSet<string> _teamFree = new(["team-list", "team-migrate", "version"], StringComparer.Ordinal);

    // The hook commands run for every session of the box, so they pass through when the team can't be told.
    private static readonly HashSet<string> _hooks = new(["stop-check", "outbox-record"], StringComparer.Ordinal);

    internal static IReadOnlyList<CommandEntry> Entries => _entries;

    private static string Ambiguity(TeamLayout layout) =>
        $"several teams exist ({string.Join(", ", layout.Ambiguous)}) and none is selected; pass --team <name> or set AW_META_TEAM (aw-meta team-list shows them)";

    // The team checks before a command runs: an unusable --team name is a usage error; several teams with none selected refuses (a hook passes).
    private static int? TeamGate(CommandEntry entry, TeamLayout layout, ConsoleIo io)
    {
        // A hook runs for every session, so a bad name or no way to tell the team lets it through, as with no team set up.
        if (_hooks.Contains(entry.Name) && (layout.Problem is not null || layout.Ambiguous.Count > 0))
        {
            return CommandOutput.Ok;
        }

        if (layout.Problem is { } bad)
        {
            return CommandOutput.Usage(io, bad, entry.Spec.Usage);
        }

        if (layout.Ambiguous.Count == 0 || _teamFree.Contains(entry.Name))
        {
            return null;
        }

        io.Err.WriteLine($"{MetaErrors.TeamAmbiguous.Code}: {Ambiguity(layout)}");
        return CommandOutput.Failed;
    }

    internal static async Task<int> Run(string[] args, IServiceProvider provider, JsonSerializerOptions json, ConsoleIo io, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0)
        {
            return CommandOutput.Usage(io, "a command is required", "<command> [arguments] (" + string.Join(", ", _entries.Select(e => e.Name)) + ")");
        }

        if (_entries.FirstOrDefault(e => e.Name == args[0]) is not { } entry)
        {
            return CommandOutput.Usage(io, $"unknown command '{args[0]}'", "<command> [arguments]");
        }

        if (CommandLine.Parse(args[1..], entry.Spec, out var problem) is not { } line)
        {
            return CommandOutput.Usage(io, problem, entry.Spec.Usage);
        }

        if (TeamGate(entry, provider.GetRequiredService<TeamLayout>(), io) is { } refused)
        {
            return refused;
        }

        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var context = new CommandContext(line, scope.ServiceProvider, json, io, cancellationToken);
            if (entry.NeedsConfig && (await scope.ServiceProvider.GetRequiredService<IMetaConfig>().Load(CancellationToken.None).ConfigureAwait(false)) is { IsError: true } missing)
            {
                return CommandOutput.Fail(context, missing.Error);
            }

            return await entry.Run(context).ConfigureAwait(false);
        }
    }
}
