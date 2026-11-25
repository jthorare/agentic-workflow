using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Evolve;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Evolve;

/// <summary>Saving: a new row, a merge into the open row with the same target and title, or nothing when the owner rejected it before.</summary>
internal sealed partial class SqliteProposalStore
{
    public async ValueTask<Result<ProposalSaved>> Save(ProposalToSave proposal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var begun = await Begin(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var session = begun.Value;
            var title = ProposalTitles.Normalized(proposal.Original.Title);
            var same = await Rows(session, new SqlStatement(
                ProposalSql.Select + " WHERE p.target = $target AND p.norm_title = $title ORDER BY p.created_at DESC, p.id DESC",
                c => c.Bind("$target", proposal.Original.Target).Bind("$title", title)), cancellationToken).ConfigureAwait(false);
            if (same.IsError)
            {
                return same.Error;
            }

            var open = same.Value.FirstOrDefault(static p => p.State is not (ProposalState.Merged or ProposalState.Rejected));
            if (open is null && same.Value.FirstOrDefault(static p => p.State == ProposalState.Rejected) is { } rejected)
            {
                return new ProposalSaved(rejected.Id, ProposalSaveOutcome.PreviouslyRejected);   // why: the session closes without a commit, so nothing is written
            }

            var saved = open is null
                ? await Insert(session, proposal, title, cancellationToken).ConfigureAwait(false)
                : await Merge(session, open, proposal, cancellationToken).ConfigureAwait(false);
            if (saved.IsError)
            {
                return saved.Error;
            }

            var committed = await session.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? LedgerDatabase.Translate(committed.Error) : saved;
        }
    }

    private async ValueTask<Result<ProposalSaved>> Insert(SqliteUnitOfWork session, ProposalToSave proposal, string title, CancellationToken cancellationToken)
    {
        var id = ProposalId.New(ids);
        var now = ProposalSql.Stamp(clock.GetUtcNow());
        var original = proposal.Original;
        var insert = new SqlStatement(
            "INSERT INTO proposal (id, kind, target, norm_title, state, tier, attempts, held_by, body, origin, draft_branch, draft_commit, created_at, updated_at) " +
            "VALUES ($id, $kind, $target, $title, $state, $tier, 0, NULL, $body, $origin, $branch, $commit, $at, $at)",
            c => c.Bind("$id", id.Value).Bind("$kind", ProposalSql.Name(original.Kind)).Bind("$target", original.Target).Bind("$title", title)
                .Bind("$state", ProposalSql.Name(ProposalState.Proposed)).Bind("$tier", ProposalSql.Name(proposal.Tier)).Bind("$body", ProposalSql.Write(original))
                .Bind("$origin", proposal.Origin).Bind("$branch", proposal.Draft?.Branch).Bind("$commit", proposal.Draft?.Commit).Bind("$at", now),
            $"proposal {id.Value}");
        var written = await SqliteQueries.Execute(session, insert, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return LedgerDatabase.Translate(written.Error);
        }

        var sources = await AddSources(session, id, proposal.Sources, cancellationToken).ConfigureAwait(false);
        return sources.IsError ? sources.Error : new ProposalSaved(id, ProposalSaveOutcome.Saved);
    }

    // why: a re-proposal adds its evidence (the newest 20 kept) and its sessions, so the gate later checks every turn the proposal was built from;
    // its draft, when it has one, is the newer edit and replaces the stored pointer, and a re-proposal without one keeps it
    private async ValueTask<Result<ProposalSaved>> Merge(SqliteUnitOfWork session, StoredProposal open, ProposalToSave proposal, CancellationToken cancellationToken)
    {
        var evidence = open.Original.Evidence.Concat(proposal.Original.Evidence).Distinct(StringComparer.Ordinal).ToList();
        var body = open.Original with { Evidence = [.. evidence.Skip(Math.Max(0, evidence.Count - MaxEvidence))] };
        var draft = proposal.Draft ?? open.Draft;
        var update = new SqlStatement(
            "UPDATE proposal SET body = $body, draft_branch = $branch, draft_commit = $commit, updated_at = $at WHERE id = $id",
            c => c.Bind("$body", ProposalSql.Write(body)).Bind("$branch", draft?.Branch).Bind("$commit", draft?.Commit)
                .Bind("$at", ProposalSql.Stamp(clock.GetUtcNow())).Bind("$id", open.Id.Value),
            $"proposal {open.Id.Value}");
        var written = await SqliteQueries.Execute(session, update, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return LedgerDatabase.Translate(written.Error);
        }

        var sources = await AddSources(session, open.Id, proposal.Sources, cancellationToken).ConfigureAwait(false);
        return sources.IsError ? sources.Error : new ProposalSaved(open.Id, ProposalSaveOutcome.Merged);
    }

    // why: a session already recorded keeps its turns and gains the new ones in order, so a source is only ever added to
    private static async ValueTask<Result<Unit>> AddSources(SqliteUnitOfWork session, ProposalId id, IReadOnlyList<LeakSource> sources, CancellationToken cancellationToken)
    {
        foreach (var source in sources)
        {
            var known = await SqliteQueries.List(session, new SqlStatement(
                "SELECT turns FROM proposal_source WHERE proposal_id = $id AND session = $session",
                c => c.Bind("$id", id.Value).Bind("$session", source.Session)), ProposalSql.TurnsMapper.Instance, cancellationToken).ConfigureAwait(false);
            if (known.IsError)
            {
                return LedgerDatabase.Translate(known.Error);
            }

            var turns = KnownTurns(known.Value);
            turns.AddRange(source.Turns.Where(t => !turns.Contains(t, StringComparer.Ordinal)));
            var upsert = new SqlStatement(
                "INSERT INTO proposal_source (proposal_id, session, turns) VALUES ($id, $session, $turns) ON CONFLICT(proposal_id, session) DO UPDATE SET turns = $turns",
                c => c.Bind("$id", id.Value).Bind("$session", source.Session).Bind("$turns", ProposalSql.Write(turns)),
                $"proposal {id.Value} source");
            var written = await SqliteQueries.Execute(session, upsert, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return LedgerDatabase.Translate(written.Error);
            }
        }

        return Unit.Value;
    }

    // why: a stored list that no longer parses counts as empty, so the session's new turns still get recorded
    private static List<string> KnownTurns(IReadOnlyList<string> rows)
    {
        if (rows.Count == 0 || ProposalSql.Read<List<string>>(rows[0]) is not { IsError: false } known)
        {
            return [];
        }

        return known.Value;
    }
}
