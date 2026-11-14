using System.Globalization;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Health;

/// <summary>Folds the status inputs into one <see cref="HealthReport"/> (UI spec E-14; P2 plan G10): stalled &gt; rebuilding &gt; degraded &gt; healthy.</summary>
public static class HealthSummary
{
    private const string SchedulerCode = "SND-SCHED-001";

    /// <summary>The health for these inputs.</summary>
    /// <param name="running">Whether this process holds the lease.</param>
    /// <param name="views">The projector's status.</param>
    /// <param name="services">The services view's rows, or null when the views can't be read.</param>
    /// <param name="context">The code index's problems and the clock's reading; none by default.</param>
    /// <returns>The health.</returns>
    public static HealthReport For(bool running, ViewsStatus views, IReadOnlyList<ServiceRow>? services, HealthContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(views);
        List<HealthProblem> problems = [];
        if (!running)
        {
            problems.Add(new("SND-API-001", "Sindri isn't running: run `sindri`", "Sindri isn't running"));
        }

        problems.AddRange(Views(views));
        if (services is null && !views.Stalled)
        {
            problems.Add(new("SND-VIEW-001", EmptyText.Unreadable(), "the views can't be read"));
        }

        problems.AddRange(FailedServices(services, context));
        var word = views.Stalled ? HealthWord.Stalled : views.Rebuilding || views.Starting ? HealthWord.Rebuilding : (problems.Count > 0 ? HealthWord.Degraded : HealthWord.Healthy);
        return new HealthReport(word, [.. problems], Sentence(word, problems));
    }

    /// <summary>A report that also counts the engine's stuck instances (API spec §3.8, E-30): any make a healthy report degraded, and join its problems and sentence.</summary>
    /// <param name="report">The report <see cref="For"/> folded.</param>
    /// <param name="stuck">The instances that only wait on a signal or update older than five minutes, with no timer.</param>
    /// <returns>The report; the same one when none are stuck.</returns>
    public static HealthReport WithStuck(HealthReport report, int stuck)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (stuck <= 0)
        {
            return report;
        }

        var clause = stuck == 1 ? "1 workflow instance is stuck waiting" : string.Create(CultureInfo.InvariantCulture, $"{stuck} workflow instances are stuck waiting");
        var problems = report.Problems.Append(new HealthProblem("SND-ENGINE-002", $"{clause} on a signal or update with no timer: open the engine screen", clause)).ToList();
        var word = report.Word == HealthWord.Healthy ? HealthWord.Degraded : report.Word;
        return new HealthReport(word, problems, Sentence(word, problems));
    }

    /// <summary>A report that also names a stale build (#1032): the running control plane is not the build 'bin/cs/current' names.</summary>
    /// <param name="report">The report <see cref="For"/> folded.</param>
    /// <param name="running">The stamp this process runs from, or null.</param>
    /// <param name="installed">The stamp 'bin/cs/current' names, or null.</param>
    /// <returns>The report; the same one when the build is not known to be stale.</returns>
    public static HealthReport WithStaleBuild(HealthReport report, string? running, string? installed)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!StaleBuild.Is(running, installed))
        {
            return report;
        }

        var problems = report.Problems.Append(new HealthProblem("SND-BUILD-001", StaleBuild.Sentence(running!, installed!), "the running build is not the installed one")).ToList();   // why: Is is true only when both are known
        var word = report.Word == HealthWord.Healthy ? HealthWord.Degraded : report.Word;
        return new HealthReport(word, problems, Sentence(word, problems));
    }

    /// <summary>A report that also counts the agent runs whose session ended its turn without a report and is still not ended (issue #1000): any make a healthy report degraded, and join its problems and sentence.</summary>
    /// <param name="report">The report so far.</param>
    /// <param name="stalled">The agent runs idle for <see cref="Launches.LaunchRules.IdleAfter"/> or longer whose session is still live.</param>
    /// <returns>The report; the same one when none are stalled.</returns>
    public static HealthReport WithStalledAgents(HealthReport report, int stalled)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (stalled <= 0)
        {
            return report;
        }

        var clause = stalled == 1 ? "1 agent run ended its turn and has not reported" : string.Create(CultureInfo.InvariantCulture, $"{stalled} agent runs ended their turn and have not reported");
        var problems = report.Problems.Append(new HealthProblem("SND-ENGINE-004", $"{clause}: open Needs you", clause)).ToList();
        var word = report.Word == HealthWord.Healthy ? HealthWord.Degraded : report.Word;
        return new HealthReport(word, problems, Sentence(word, problems));
    }

    /// <summary>A report that also counts the agent sessions that sat idle and were ended with no report, and no newer launch took the Step over (issue #1265): any make a healthy report degraded.</summary>
    /// <param name="report">The report so far.</param>
    /// <param name="idle">The stalled launches, read from the launches view; they are ended, so none is also counted by <see cref="WithStalledAgents"/>.</param>
    /// <returns>The report; the same one when none are stalled.</returns>
    public static HealthReport WithIdleAgents(HealthReport report, int idle)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (idle <= 0)
        {
            return report;
        }

        var clause = idle == 1 ? "1 agent idle, no report" : string.Create(CultureInfo.InvariantCulture, $"{idle} agents idle, no report");
        var problems = report.Problems.Append(new HealthProblem("SND-ENGINE-005", $"{clause}: its session was ended and the Step has not run again (sindri doctor names it)", clause)).ToList();
        var word = report.Word == HealthWord.Healthy ? HealthWord.Degraded : report.Word;
        return new HealthReport(word, problems, Sentence(word, problems));
    }

    /// <summary>
    /// A report that also names a live profile that won't load (#3436): the file is there but is invalid or can't be read, so Sindri runs with no
    /// repos. It makes a healthy report degraded. No profile file at all is a fresh home, not a fault: pass false for it.
    /// </summary>
    /// <param name="report">The report so far.</param>
    /// <param name="wontLoad">True when the profile file is there and won't load (status's problemKind 'invalid').</param>
    /// <param name="fix">What is wrong with the file and how to fix it, in the reader's words; null for a plain fallback. The row names the fix, never a page.</param>
    /// <returns>The report; the same one when the profile loads or is missing.</returns>
    public static HealthReport WithProfileProblem(HealthReport report, bool wontLoad, string? fix)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!wontLoad)
        {
            return report;
        }

        const string clause = "your profile won't load";
        var problems = report.Problems.Append(new HealthProblem(SindriErrors.ProfileInvalid.Code, "Your profile won't load, so Sindri runs with no repos. " + (string.IsNullOrWhiteSpace(fix) ? "Fix the file or its permissions." : fix), clause)).ToList();
        var word = report.Word == HealthWord.Healthy ? HealthWord.Degraded : report.Word;
        return new HealthReport(word, problems, Sentence(word, problems));
    }

    private static IEnumerable<HealthProblem> Views(ViewsStatus views)
    {
        if (views.Stalled)
        {
            yield return views.StalledAt is { } at
                ? new("SND-VIEW-001", EmptyText.Stalled(at), string.Create(CultureInfo.InvariantCulture, $"the views stopped at event {at}"))
                : new("SND-VIEW-001", EmptyText.Failing(views.StalledType), "the views can't be updated");
        }
        else if (views.Rebuilding)
        {
            yield return new("SND-API-010", EmptyText.Rebuilding(views.Cursor, views.LedgerHead),
                string.Create(CultureInfo.InvariantCulture, $"the views are replaying (cursor {views.Cursor} of {views.LedgerHead})"));
        }
        else if (views.Starting)
        {
            yield return new("SND-API-010", EmptyText.Starting(), "the views are starting");
        }
    }

    // The services whose last run failed, then the code index's problems (they come before the profile's).
    private static IEnumerable<HealthProblem> FailedServices(IReadOnlyList<ServiceRow>? services, HealthContext? context) =>
        (services ?? []).Where(s => s.LastRun is { Outcome: ServiceOutcome.Failed }).Select(s => Failed(s.Name, s.LastRun, context?.Now)).Concat(context?.CodeIndex ?? []);

    private static HealthProblem Failed(ServiceName name, LastRun? run, DateTimeOffset? now)
    {
        var when = run is null || now is not { } at ? string.Empty : " " + AgeWords.Ago(at - run.Slot);
        return new(run?.Code ?? SchedulerCode, $"Service {name.Value} failed{when}", $"service {name.Value} failed");
    }

    private static string? Sentence(HealthWord word, List<HealthProblem> problems)
    {
        if (problems.Count == 0)
        {
            return null;
        }

        var clauses = problems.ConvertAll(p => p.Clause);
        var joined = clauses.Count switch
        {
            1 => clauses[0],
            2 => $"{clauses[0]} and {clauses[1]}",
            _ => $"{string.Join(", ", clauses.Take(clauses.Count - 1))}, and {clauses[^1]}",
        };
        return $"Sindri is {word.ToString().ToLowerInvariant()}: {joined}.";
    }
}
