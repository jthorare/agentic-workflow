using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.PinConcepts;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PinConcepts;

/// <summary>PinConcepts over in-memory ports: the slice harness (ledger, lease, caller, events), a profile file, a stub writer and a stub map.</summary>
internal sealed class PinConceptsRig : IDisposable
{
    private readonly List<IDisposable> _entered = [];
    private bool _leased;

    internal PinConceptsRig()
    {
        Recorder = new PinEvents(Slice.Held, Slice.Store, Slice.Events, Slice.Callers);
        _entered.Add(Slice.Callers.EnterService("test"));
        Profile();
    }

    internal SliceHarness Slice { get; } = new();

    internal FakeProfileIndexWriter Writer { get; } = new();

    /// <summary>Every edit written, in order.</summary>
    internal IReadOnlyList<ConceptEdit> Edits => [.. Writer.Writes.SelectMany(static w => w).OfType<ConceptEdit>()];

    internal MapStub Map { get; } = new();

    internal PinEvents Recorder { get; }

    internal Lines Progress { get; } = new();

    internal FakeJobContextAccessor Jobs { get; } = new();

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Holds the lease, once.</summary>
    internal async Task Lease()
    {
        if (!_leased)
        {
            _ = await Slice.HoldLease();
            _leased = true;
        }
    }

    internal PinConceptsService Service()
    {
        _entered.Add(Jobs.Enter(new JobContext(JobId.From("JOB-" + new string('a', 32)), JobKind.From("PinConcepts"), Progress)));
        return new PinConceptsService(Slice.Profiles, Map, AfterWrite is null ? Writer : new Then(Writer, AfterWrite), Recorder, Jobs);
    }

    /// <summary>Runs after each successful write, before the event is recorded.</summary>
    internal Action? AfterWrite { get; set; }

    internal PinConceptsAdmission Admission() => new(Slice.Profiles);

    /// <summary>The caller of the calls that follow.</summary>
    internal void CallerIs(Core.Commands.CallerContext caller) => _entered.Add(Slice.Callers.Enter(caller));

    /// <summary>The live profile names these top-level pins and these repos.</summary>
    internal void Profile(IReadOnlyList<ConceptSettings>? pins = null, params RepoEntry[] repos)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Index: pins is null ? null : new IndexSettings(Concepts: pins), Repos: repos.Length == 0 ? null : repos)).Value;
        Slice.Profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(Kernel.Json.AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    public void Dispose()
    {
        foreach (var entered in _entered)
        {
            entered.Dispose();
        }
    }

    internal sealed class Lines : IProgress<string>
    {
        private readonly List<string> _seen = [];

        internal IReadOnlyList<string> Seen => _seen;

        public void Report(string value) => _seen.Add(value);
    }

    private sealed class Then(IProfileIndexWriter inner, Action after) : IProfileIndexWriter
    {
        public async ValueTask<Result<ProfileHash>> Write(IReadOnlyList<IndexEdit> edits, ProfileHash? ifHash, CancellationToken cancellationToken)
        {
            var written = await inner.Write(edits, ifHash, cancellationToken).ConfigureAwait(false);
            after();
            return written;
        }
    }

    /// <summary>A map that holds some features at a version, or no map at all.</summary>
    internal sealed class MapStub : IMapFeatures
    {
        internal string? Version { get; set; } = "v41";

        internal HashSet<string> Has { get; } = [];

        internal int Asked { get; private set; }

        public ValueTask<Result<MapFeatureCheck>> Check(IReadOnlyList<string> keys, CancellationToken cancellationToken)
        {
            Asked++;
            return ValueTask.FromResult<Result<MapFeatureCheck>>(new MapFeatureCheck(Version: Version, Missing: [.. keys.Where(k => Version is null || !Has.Contains(k))]));
        }
    }
}
