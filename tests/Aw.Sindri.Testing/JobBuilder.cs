using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Testing;

/// <summary>Builds a jobs-view row with deterministic defaults: A.FirstJobId, a running RunService job keyed 'heartbeat' for the heartbeat service, started at the epoch by 'human:cli'.</summary>
public sealed class JobBuilder
{
    private JobRow _row = new(A.FirstJobId, JobKind.From("RunService"), JobKey.From("heartbeat"), Item: null, Repo: null, ServiceName.From("heartbeat"),
        JobState.Running, "human:cli", KnownInstants.Epoch, EndedAt: null, Progress: [], Rounds: 0, Summary: null, Code: null, Result: null, CancelledBy: null, Sequence: 1);

    /// <summary>Sets the id.</summary>
    /// <param name="value">The job.</param>
    /// <returns>This builder.</returns>
    public JobBuilder WithId(JobId value)
    {
        _row = _row with { Id = value };
        return this;
    }

    /// <summary>Sets the kind and key, and clears the service.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="key">The key.</param>
    /// <returns>This builder.</returns>
    public JobBuilder OfKind(JobKind kind, JobKey key)
    {
        _row = _row with { Kind = kind, Key = key, Service = null };
        return this;
    }

    /// <summary>Sets the repo.</summary>
    /// <param name="value">The repo, or null.</param>
    /// <returns>This builder.</returns>
    public JobBuilder InRepo(RepoName? value)
    {
        _row = _row with { Repo = value };
        return this;
    }

    /// <summary>Ends the job in <paramref name="state"/> one minute after it started.</summary>
    /// <param name="state">A terminal state.</param>
    /// <returns>This builder.</returns>
    public JobBuilder Ended(JobState state)
    {
        _row = _row with { State = state, EndedAt = _row.StartedAt.AddMinutes(1), Summary = state switch { JobState.Succeeded => "succeeded", JobState.Failed => "failed", JobState.Cancelled => "cancelled", JobState.Running => null, JobState.Waiting => null, _ => null } };
        return this;
    }

    /// <summary>Gives the job <paramref name="count"/> progress lines, 'line 1' on.</summary>
    /// <param name="count">How many.</param>
    /// <returns>This builder.</returns>
    public JobBuilder WithProgress(int count)
    {
        _row = _row with
        {
            Progress = [.. Enumerable.Range(1, count).Select(n => new JobLine(n, string.Create(CultureInfo.InvariantCulture, $"line {n}"), _row.StartedAt.AddSeconds(n)))],
            Rounds = count,
        };
        return this;
    }

    /// <summary>Builds the row.</summary>
    /// <returns>The row.</returns>
    public JobRow Build() => _row;
}
