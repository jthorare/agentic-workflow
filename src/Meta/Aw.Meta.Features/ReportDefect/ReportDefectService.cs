using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Defects;
using Aw.Meta.Core.Github;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.ReportDefect;

/// <summary>Files a toolkit defect as a GitHub issue, or comments on the one already open for it.</summary>
/// <param name="config">The team config: the issue repo, the daily cap, the repo directory and the meta lane.</param>
/// <param name="repo">Runs gh.</param>
/// <param name="values">Holds today's counts.</param>
/// <param name="log">The running log.</param>
/// <param name="owner">The owner's queue, for the FYI.</param>
/// <param name="clock">The current time.</param>
internal sealed class ReportDefectService(IMetaConfig config, RepoCommands repo, IKeyValueStore values, IRunningLog log, IOwnerStore owner, TimeProvider clock) : IService<ReportDefectRequest, ReportDefectResponse>
{
    public async ValueTask<Result<ReportDefectResponse>> Handle(ReportDefectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await config.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var team = loaded.Value;
        var now = clock.GetUtcNow();
        var title = DefectText.Title(request.Code, request.Title);
        var body = DefectText.Body(request.What, request.ErrorLine, request.Path);
        var search = await repo.Gh(team.Repo, "issue", "list", "-R", team.Defects.Repo, "--state", "all", "--search", $"\"{title}\" in:title", "--limit", "50", "--json", "number,title,state,closedAt,url").ConfigureAwait(false);
        if (search.IsError)
        {
            return search.Error;
        }

        var issues = GhIssues.List(search.Value);
        if (issues is null)
        {
            return MetaErrors.CommandFailed.With("gh issue list printed something unexpected");
        }

        return DefectText.Match(issues, title, now) is { } seen
            ? await Comment(team, seen, title, body, cancellationToken).ConfigureAwait(false)
            : await Fresh(team, title, body, now, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ReportDefectResponse>> Comment(TeamConfig team, IssueRef seen, string title, string body, CancellationToken cancellationToken)
    {
        var commented = await repo.Gh(team.Repo, "issue", "comment", seen.Number.ToString(CultureInfo.InvariantCulture), "-R", team.Defects.Repo, "--body", body).ConfigureAwait(false);
        if (commented.IsError)
        {
            return commented.Error;
        }

        var line = LogLine.Event(LogKind.Note, clock.GetUtcNow(), $"defect seen again, commented on {seen.Url}: {title}");
        var appended = await log.Append(line, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ReportDefectResponse("commented", seen.Url, line);
    }

    private async ValueTask<Result<ReportDefectResponse>> Fresh(TeamConfig team, string title, string body, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var stored = await values.Get(KvKeys.DefectsDay, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var (filed, skipped) = DefectDay.Parse(stored.Value, today);
        if (filed >= team.Defects.DailyCap)
        {
            return await CapHit(title, today, filed, skipped + 1, cancellationToken).ConfigureAwait(false);
        }

        var created = await repo.Gh(team.Repo, "issue", "create", "-R", team.Defects.Repo, "--title", title, "--body", body).ConfigureAwait(false);
        if (created.IsError)
        {
            return created.Error;
        }

        var url = created.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if (url is null)
        {
            return MetaErrors.CommandFailed.With("gh issue create printed something unexpected");
        }

        var recorded = await Record(DefectDay.Format(today, filed + 1, skipped), $"defect filed {url}: {title}", now, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        var asked = await owner.Ask(team.MetaLane, $"FYI, no answer needed: filed {url}: {title}", now, cancellationToken).ConfigureAwait(false);
        return asked.IsError ? asked.Error : new ReportDefectResponse("filed", url, recorded.Value);
    }

    private async ValueTask<Result<ReportDefectResponse>> CapHit(string title, DateOnly today, int filed, int skipped, CancellationToken cancellationToken)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"issue cap hit ({skipped} skipped today): {title}");
        var recorded = await Record(DefectDay.Format(today, filed, skipped), text, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new ReportDefectResponse("cap-hit", Url: null, recorded.Value, Skipped: skipped);
    }

    // Keeps today's counts, then logs the NOTE; returns the log line.
    private async ValueTask<Result<string>> Record(string day, string note, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var set = await values.Set(KvKeys.DefectsDay, day, cancellationToken).ConfigureAwait(false);
        if (set.IsError)
        {
            return set.Error;
        }

        var line = LogLine.Event(LogKind.Note, now, note);
        var appended = await log.Append(line, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : line;
    }
}
