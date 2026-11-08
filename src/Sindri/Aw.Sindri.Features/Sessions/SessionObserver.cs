using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.Sessions;

/// <summary>Turns a discovered process into the session a client sees: merged state, scrubbed title, repo, launch and lane (collector spec §4.2, §4.3, §12).</summary>
internal sealed class SessionObserver(ITranscripts transcripts, ITextScrubber scrubber, SessionRepos repos)
{
    private const int TitleLimit = 80;

    internal ObservedSession Observe(Candidate candidate, DateTimeOffset now, ObservedSession? previous)
    {
        var transcript = transcripts.LastWrite(candidate.Agent, candidate.IdKnown ? candidate.Session : null, candidate.TranscriptHint);
        var merged = SessionStateRules.Merge(new StateInputs(candidate.Record, candidate.Hook, transcript, now));
        var steady = previous is not null && previous.State == merged.State && previous.Reason == merged.Reason && merged.Source is StateSource.Transcript or StateSource.None;
        var env = candidate.Environment;
        return new ObservedSession(candidate.Ref, candidate.Process.Pid, candidate.Process.StartTicks, merged.State, merged.Source, steady ? previous!.Since : merged.Since) // why: steady implies previous is set
        {
            IdKnown = candidate.IdKnown,
            Reason = merged.Reason,
            Tool = merged.Tool,
            Estimated = merged.Estimated,
            Cwd = candidate.Cwd,
            Repo = repos.Lookup(candidate.Cwd),
            Host = candidate.Host,
            Title = Title(candidate.Record?.Name),
            LaunchId = env.TryGetValue("AW_SINDRI_LAUNCH", out var launch) && LaunchId.TryFrom(launch, out var id) ? id : null,
            Team = env.GetValueOrDefault("AW_META_TEAM"),
            Lane = env.GetValueOrDefault("AW_META_LANE"),
            StartedAt = candidate.Record?.StartedAt,
        };
    }

    private string? Title(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var scrubbed = scrubber.Scrub(name).Trim();
        return scrubbed.Length == 0 ? null : scrubbed.Length > TitleLimit ? scrubbed[..TitleLimit] : scrubbed;
    }
}
