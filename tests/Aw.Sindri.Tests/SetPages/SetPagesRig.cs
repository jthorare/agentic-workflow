using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.SetPages;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SetPages;

/// <summary>SetPages over in-memory ports: the slice harness (profile file), a stub map and the recording index writer.</summary>
internal sealed class SetPagesRig : IDisposable
{
    private readonly List<IDisposable> _entered = [];
    private bool _leased;

    internal SetPagesRig()
    {
        Recorder = new PageEvents(Slice.Held, Slice.Store, Slice.Events, Slice.Callers);
        _entered.Add(Slice.Callers.EnterService("test"));
        Profile([], new RepoEntry("web", "/work/web"));
    }

    internal SliceHarness Slice { get; } = new();

    internal FakeProfileIndexWriter Writer { get; } = new();

    /// <summary>Every page edit written, in order.</summary>
    internal IReadOnlyList<PageEdit> Edits => [.. Writer.Writes.SelectMany(static w => w).OfType<PageEdit>()];

    internal MapFactsStub Map { get; } = new();

    internal FakeJobContextAccessor Jobs { get; } = new();

    internal PageEvents Recorder { get; }

    internal Lines Progress { get; } = new();

    /// <summary>Holds the lease, once.</summary>
    internal async Task Lease()
    {
        if (!_leased)
        {
            _ = await Slice.HoldLease();
            _leased = true;
        }
    }

    internal SetPagesAdmission Admission() => new(Slice.Profiles, Map);

    /// <summary>The caller of the calls that follow.</summary>
    internal void CallerIs(Core.Commands.CallerContext caller) => _entered.Add(Slice.Callers.Enter(caller));

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    internal SetPagesService Service()
    {
        _entered.Add(Jobs.Enter(new JobContext(JobId.From("JOB-" + new string('a', 32)), JobKind.From("SetPages"), Progress)));
        return new SetPagesService(Slice.Profiles, Map, Writer, Recorder, Jobs);
    }

    /// <summary>The live profile names these top-level pages and these repos.</summary>
    internal void Profile(IReadOnlyList<PageSettings> pages, params RepoEntry[] repos)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Index: new IndexSettings(Pages: pages), Repos: repos.Length == 0 ? null : repos)).Value;
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

    /// <summary>A map holding two concepts, one surface and one action, all in the repo 'web'; a test adds to it or empties it.</summary>
    internal sealed class MapFactsStub : IMapPageFacts
    {
        internal string? Version { get; set; } = "v41";

        internal List<MapConcept> Concepts { get; } = [new("self-scheduling", "Self-scheduling"), new("approval-workflow", "Approval workflow")];

        internal Dictionary<string, IReadOnlyList<string>> Surfaces { get; } = new(StringComparer.Ordinal) { ["surface:route/GET:/schedule/week"] = ["web"] };

        internal Dictionary<string, IReadOnlyList<string>> Actions { get; } = new(StringComparer.Ordinal) { ["action:schedulepage.submit-schedule"] = ["web"] };

        public ValueTask<Result<MapPageFacts>> Read(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<MapPageFacts>>(new MapPageFacts(Version, [.. Concepts], Surfaces, Actions));
    }
}
