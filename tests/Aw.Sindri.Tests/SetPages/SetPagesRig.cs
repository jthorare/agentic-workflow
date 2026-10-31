using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;
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

    internal SetPagesRig()
    {
        Profile([], new RepoEntry("web", "/work/web"));
    }

    internal SliceHarness Slice { get; } = new();

    internal FakeProfileIndexWriter Writer { get; } = new();

    /// <summary>Every page edit written, in order.</summary>
    internal IReadOnlyList<PageEdit> Edits => [.. Writer.Writes.SelectMany(static w => w).OfType<PageEdit>()];

    internal MapFactsStub Map { get; } = new();

    internal FakeJobContextAccessor Jobs { get; } = new();

    internal static CancellationToken Token => TestContext.Current.CancellationToken;

    internal SetPagesService Service()
    {
        _entered.Add(Jobs.Enter(new JobContext(JobId.From("JOB-" + new string('a', 32)), JobKind.From("SetPages"), new Lines())));
        return new SetPagesService(Slice.Profiles, Map, Writer, Jobs);
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

    private sealed class Lines : IProgress<string>
    {
        public void Report(string value)
        {
        }
    }

    /// <summary>A map holding two concepts, one surface and one action, all in the repo 'web'.</summary>
    internal sealed class MapFactsStub : IMapPageFacts
    {
        public ValueTask<Result<PageMapFacts>> Read(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<PageMapFacts>>(new PageMapFacts(
                "v41",
                new PageFacts(
                    new HashSet<ConceptId> { ConceptId.From("self-scheduling"), ConceptId.From("approval-workflow") },
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["surface:route/GET:/schedule/week"] = ["web"] },
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["action:schedulepage.submit-schedule"] = ["web"] }),
                [new PageMapConcept(ConceptId.From("self-scheduling"), "Self-scheduling"), new PageMapConcept(ConceptId.From("approval-workflow"), "Approval workflow")]));
    }
}
