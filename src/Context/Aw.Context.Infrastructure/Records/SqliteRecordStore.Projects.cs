using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The project read (projects page spec §6.1).</summary>
internal sealed partial class SqliteRecordStore
{
    public async ValueTask<Result<IReadOnlyList<ProjectRow>>> ProjectFacts(SourceId source, RecordAudience audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<ProjectRow>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var allowed = SqliteLexicalLeg.JsonArray(audience.Allowed.Select(k => k.Value));
            var records = SqliteLexicalLeg.JsonArray(audience.Records.Select(k => k.Value));
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.ProjectFacts, c => c.Bind("@source", source.Value).Bind("@audiences", allowed).Bind("@records", records)), RecordMappers.Project, cancellationToken).ConfigureAwait(false);
            var prs = rows.IsError ? rows.Error : await SqliteQueries.List(session, new SqlStatement(RecordSql.ProjectPrs, c => c.Bind("@source", source.Value).Bind("@audiences", allowed).Bind("@records", records)), RecordMappers.ProjectPr, cancellationToken).ConfigureAwait(false);
            if (prs.IsError)
            {
                return ContextDatabase.Translate(prs.Error);
            }

            var byProject = prs.Value.ToLookup(static p => p.Project);
            return Results.Success<IReadOnlyList<ProjectRow>>([.. rows.Value.Select(row => WithPrs(row, byProject[row.Key]))]);
        }
    }

    // why: a project's PRs come from a second statement so the per-project counts stay one probe each; a PR key is 'github:pr/<owner>/<name>/<n>', so the repo is the key without its prefix and number
    private static ProjectRow WithPrs(ProjectRow row, IEnumerable<ProjectPr> prs)
    {
        var list = prs.ToList();
        return row with
        {
            OpenPrs = list.Count(static p => p.State is RecordState.Open or RecordState.Draft),
            PrRepos = [.. list.Select(static p => RepoOf(p.Pr)).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            NewestPrAt = list.Count == 0 ? null : list.Max(static p => p.UpdatedAt),
        };
    }

    private static string? RepoOf(RecordKey pr)
    {
        const string Prefix = "github:pr/";
        var key = pr.Value;
        var end = key.LastIndexOf('/');
        return key.StartsWith(Prefix, StringComparison.Ordinal) && end > Prefix.Length ? key[Prefix.Length..end] : null;
    }
}
