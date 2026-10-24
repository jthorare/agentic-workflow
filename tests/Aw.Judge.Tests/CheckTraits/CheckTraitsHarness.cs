using Aw.Judge.Core.Batching;
using Aw.Judge.Features.CheckTraits;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Infrastructure.Traits;
using Aw.Judge.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.CheckTraits;

/// <summary>The trait-check service over a <see cref="TraitRepo"/> and a stub Jev: the real git, trait files and trait parser; the redactor, config store and log are fakes.</summary>
internal sealed class CheckTraitsHarness : IDisposable
{
    private readonly TempStateDirectory _state = new();

    public CheckTraitsHarness(TraitRepo repo, FakeBatchClassifier jev)
    {
        Repo = repo;
        Jev = jev;
        Service = new CheckTraitsService(
            new SystemProcessRunner(Environment.GetEnvironmentVariable),
            new FileTraitStore(Path.Combine(_state.Path, "traits")),
            new FileTraitFiles(repo.Root),
            new JevTraitJudge(jev, new FakeInputRedactor(), new InMemoryJudgeConfigStore()),
            Log,
            new FakeTimeProvider());
    }

    public TraitRepo Repo { get; }

    public FakeBatchClassifier Jev { get; }

    public RecordingTraitLog Log { get; } = new();

    public CheckTraitsService Service { get; }

    /// <summary>The overlay directory of the repo's traits ('&lt;state&gt;/traits/&lt;repo-name&gt;').</summary>
    public string Overlay => Path.Combine(_state.Path, "traits", Path.GetFileName(Repo.Root));

    /// <summary>A stub Jev that says every asked trait is broken with this probability.</summary>
    public static FakeBatchClassifier Violating(double p = 0.9) => new(request => new BatchDecided(
        request.Questions.ToDictionary(
            question => question.Key,
            question => (BatchAnswer)new ChoiceBatchAnswer("violation", p, new Dictionary<string, double>(StringComparer.Ordinal) { ["violation"] = p }),
            StringComparer.Ordinal)));

    public void Dispose() => _state.Dispose();
}
