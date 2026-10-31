using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The relink job rows and the starter the tick tests share: a succeeded relink for a pair stands for "the records were linked for these aliases on this map".</summary>
internal static class RelinkRig
{
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    /// <summary>The hash of a profile that pins nothing (every tick test's profile).</summary>
    internal static string NoPinsHash { get; } = PinAliasSet.Hash([]);

    internal static JobKind Kind { get; } = JobKind.From("RelinkPinAliases");

    /// <summary>Puts a relink job that ended <paramref name="endedAgo"/> ago; a succeeded one keeps the result for the pair.</summary>
    internal static void Put(InMemoryJobViews jobs, SliceHarness harness, DateTimeOffset now, JobState state, TimeSpan endedAgo, string hash, int version, long sequence, string? code = null)
    {
        var result = state == JobState.Succeeded
            ? JsonSerializer.SerializeToElement(new RelinkPinAliasesJobResult(hash, version, Records: 4, Changed: 1, ElapsedMs: 12, [new PinLinkCount(ConceptId.From("payroll"), 1, 2)]), _web)
            : (JsonElement?)null;
        jobs.Put(new JobRow(JobId.New(harness.Ids), Kind, JobKey.From("relink"), Item: null, Repo: null, Service: null, state,
            "service:context-map-refresh", now - endedAgo - TimeSpan.FromSeconds(5), state == JobState.Running ? null : now - endedAgo, [], Rounds: 0, Summary: null,
            code ?? (state == JobState.Failed ? "SND-CONTEXT-030" : null), result, CancelledBy: null, Sequence: sequence));
    }
}
