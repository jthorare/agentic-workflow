using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The work of a relink job (concept pins spec 3.4): one engine call, its counts as the job's result, and one progress line.</summary>
public sealed class RelinkPinAliasesServiceTests
{
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);
    private readonly SliceHarness _h = new();
    private readonly RelinkEngineProxy _engine = RelinkEngineProxy.Create();
    private readonly FakeJobContextAccessor _jobs = new();
    private readonly List<string> _progress = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<RelinkPinAliasesJobResult>> Run()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var service = new RelinkPinAliasesService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _jobs);
        using var job = _jobs.Enter(new JobContext(JobId.New(_h.Ids), JobKind.From("RelinkPinAliases"), new Progress(_progress)));
        using var caller = _h.Callers.EnterService("context-map-refresh");
        return await service.Handle(new RelinkPinAliasesRequest(), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_engines_counts_become_the_jobs_result()
    {
        var result = await Run();

        Assert.Equal(new RelinkPinAliasesJobResult("h1", MapVersion: 3, Records: 40, Changed: 2, ElapsedMs: 150, [new PinLinkCount(ConceptId.From("payroll"), 4, 7), new PinLinkCount(ConceptId.From("rota"), 0, 1)]), result.Value, new ResultComparer());
        Assert.Equal(1, _engine.Relinks);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_result_serialises_with_the_fields_the_tick_reads()
    {
        var result = await Run();

        var json = JsonSerializer.SerializeToElement(result.Value, _web);
        Assert.Equal("h1", json.GetProperty("aliasesHash").GetString());
        Assert.Equal(3, json.GetProperty("mapVersion").GetInt32());
        Assert.Equal(40, json.GetProperty("records").GetInt32());
        Assert.Equal(2, json.GetProperty("changed").GetInt32());
        Assert.Equal(150, json.GetProperty("elapsedMs").GetInt64());
        Assert.Equal(7, json.GetProperty("pins")[0].GetProperty("after").GetInt32());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Progress_says_what_the_job_is_doing()
    {
        _ = await Run();

        Assert.Equal("linking records to its words", Assert.Single(_progress));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_engine_error_fails_the_job()
    {
        _engine.Answer = SindriErrors.InvalidInput.With("x");

        var result = await Run();

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    private sealed class Progress(List<string> lines) : IProgress<string>
    {
        public void Report(string value) => lines.Add(value);
    }

    // The pins list compares by reference in a record's equality: compare the fields.
    private sealed class ResultComparer : IEqualityComparer<RelinkPinAliasesJobResult>
    {
        public bool Equals(RelinkPinAliasesJobResult? x, RelinkPinAliasesJobResult? y) =>
            x is not null && y is not null && x.AliasesHash == y.AliasesHash && x.MapVersion == y.MapVersion && x.Records == y.Records && x.Changed == y.Changed
            && x.ElapsedMs == y.ElapsedMs && x.Pins.SequenceEqual(y.Pins);

        public int GetHashCode(RelinkPinAliasesJobResult obj) => obj.AliasesHash.GetHashCode(StringComparison.Ordinal);
    }
}
