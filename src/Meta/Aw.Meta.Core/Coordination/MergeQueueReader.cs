using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Reads the merge queue: the pull requests lanes said are ready that are not yet merged, ended on GitHub or dropped. Status
/// lists it and the heavy reservation takes its depth.
/// </summary>
/// <param name="messages">The inbox.</param>
/// <param name="gates">The gate runs and merges.</param>
/// <param name="values">Holds the pull requests the watch saw end, the queue entries dropped and the pull requests handed to GitHub's merge queue.</param>
/// <param name="repo">Reads GitHub's merge queue.</param>
/// <param name="config">The team config, for the repo.</param>
public sealed class MergeQueueReader(IMessageStore messages, IGateStore gates, IKeyValueStore values, RepoCommands repo, IMetaConfig config)
{
    /// <summary>The same reads over other gh calls (a poll's, bound by its limit and the stop signal).</summary>
    /// <param name="github">The calls to use.</param>
    /// <returns>A reader over them.</returns>
    public MergeQueueReader Using(RepoCommands github) => new(messages, gates, values, github, config);

    /// <summary>The queue as it stands, lowest pull request number first.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="withGithub">Whether to ask GitHub where each pull request in its merge queue stands (one gh call each); false leaves QueuedPr.Github empty.</param>
    /// <returns>The queued pull requests, or the store's error.</returns>
    public async ValueTask<Result<IReadOnlyList<QueuedPr>>> Read(CancellationToken cancellationToken, bool withGithub = false)
    {
        var ended = await values.Get(KvKeys.EndedPrs, cancellationToken).ConfigureAwait(false);
        var dropped = await values.Get(KvKeys.QueueDropped, cancellationToken).ConfigureAwait(false);
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (ended.IsError || dropped.IsError || inbound.IsError)
        {
            return ended.IsError ? ended.Error : dropped.IsError ? dropped.Error : inbound.Error;
        }

        var queued = new List<QueuedPr>();
        foreach (var (pr, from) in Ready(inbound.Value, EndedPrs.Parse(ended.Value), QueueDrops.Parse(dropped.Value)))
        {
            var entry = await Entry(pr, from, cancellationToken).ConfigureAwait(false);
            if (entry.IsError)
            {
                return entry.Error;
            }

            if (entry.Value is { } queuedPr)
            {
                queued.Add(queuedPr);
            }
        }

        return withGithub ? await WithGithub(queued, cancellationToken).ConfigureAwait(false) : queued;
    }

    // Only the pull requests merge-run handed to GitHub's merge queue cost a gh call.
    private async ValueTask<Result<IReadOnlyList<QueuedPr>>> WithGithub(List<QueuedPr> queued, CancellationToken cancellationToken)
    {
        var handed = await values.Get(KvKeys.MergeQueued, cancellationToken).ConfigureAwait(false);
        if (handed.IsError)
        {
            return handed.Error;
        }

        var inGithub = MergeQueued.Parse(handed.Value);
        var result = new List<QueuedPr>();
        foreach (var entry in queued)
        {
            result.Add(inGithub.ContainsKey(entry.Pr) ? entry with { Github = await GithubText(entry.Pr, cancellationToken).ConfigureAwait(false) } : entry);
        }

        return result;
    }

    // "queued 2 (AWAITING_CHECKS)" from GitHub now; "queued (unknown)" when the read fails.
    private async ValueTask<string> GithubText(int pr, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        var view = team.IsError ? Results.Failure<MergeQueueView>(team.Error) : await repo.QueueView(team.Value.Repo, pr).ConfigureAwait(false);
        if (view.IsError)
        {
            return "queued (unknown)";
        }

        var place = view.Value.Position is { } number ? " " + number.ToString(CultureInfo.InvariantCulture) : string.Empty;
        return view.Value.EntryState is { } state ? $"queued{place} ({state})" : $"queued{place}";
    }

    // A ready pull request with its latest gate verdict; null once any merge is recorded for it.
    private async ValueTask<Result<QueuedPr?>> Entry(int pr, string from, CancellationToken cancellationToken)
    {
        var merge = await gates.FindMerge(pr, cancellationToken).ConfigureAwait(false);
        if (merge.IsError || merge.Value is not null)
        {
            return merge.IsError ? merge.Error : Results.Success<QueuedPr?>(value: null);
        }

        var gate = await gates.Latest(pr, cancellationToken).ConfigureAwait(false);
        return gate.IsError ? gate.Error : Results.Success<QueuedPr?>(new QueuedPr(pr, from, gate.Value?.Verdict.ToString() ?? "none"));
    }

    // The latest "ready #n" message per pull request number, lowest number first, leaving out those GitHub shows merged or closed (the caller's set) and those whose entry was dropped since the message.
    private static IEnumerable<(int Pr, string From)> Ready(IReadOnlyList<InboxMessage> inbound, IReadOnlySet<int> ended, IReadOnlyDictionary<int, DateTimeOffset> dropped) =>
        inbound
            .Where(m => m.Class is { Kind: MessageKind.Ready, Pr: { } pr } && !ended.Contains(pr) && !(dropped.TryGetValue(pr, out var at) && m.At <= at))
            .GroupBy(m => m.Class.Pr.GetValueOrDefault())
            .OrderBy(group => group.Key)
            .Select(group => group.MaxBy(m => m.At))
            .OfType<InboxMessage>()
            .Select(m => (m.Class.Pr.GetValueOrDefault(), m.FromName.Length > 0 ? m.FromName : m.From));
}
