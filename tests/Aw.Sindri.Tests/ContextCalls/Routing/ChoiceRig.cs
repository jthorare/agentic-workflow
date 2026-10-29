using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Config;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The Jev choice port over a scripted classifier, with the approved settings, a job context and the event log in reach.</summary>
internal sealed class ChoiceRig
{
    private readonly FakeJobContextAccessor _jobs = new();

    public ChoiceRig(ScriptedBatches batches, LabelPick pick = LabelPick.Jev, ProvidersConfig? providers = null)
    {
        Batches = batches;
        Models = new SwitchableModels(pick);
        Port = new JevChoicePort(() => batches, _ => ValueTask.FromResult(providers), Models, _jobs, Calls, new SequentialIdGenerator(), Clock);
    }

    public ScriptedBatches Batches { get; }

    public SwitchableModels Models { get; }

    public RecordedCalls Calls { get; } = new();

    public FakeTimeProvider Clock { get; } = new();

    public JevChoicePort Port { get; }

    public static ChoiceQuestion Question(string key, params string[] options) =>
        new(key, "Pick the name that best describes the feature these source files belong to.\nFiles: src/" + key + "/a.ts", options.Length > 0 ? options : ["Alpha", "Beta"]);

    public IDisposable EnterJob() =>
        _jobs.Enter(new JobContext(JobId.From("JOB-" + new string('a', 32)), JobKind.From("BuildContextMap"), new Progress<string>(), Waiting: null));

    public ValueTask<Aw.Kernel.Result<IReadOnlyList<ChoiceAnswer>>> Pick(params ChoiceQuestion[] questions) =>
        Port.Pick(ChoiceKind.Labeling, questions, capChars: 160_000, TestContext.Current.CancellationToken);

    /// <summary>The approved 'context.models', which a test can change between calls the way an approval does.</summary>
    internal sealed class SwitchableModels(LabelPick pick) : IApprovedModels
    {
        public LabelPick Pick { get; set; } = pick;

        public ValueTask<ModelsSettings> Current(CancellationToken cancellationToken) => ValueTask.FromResult(new ModelsSettings { LabelPick = Pick });
    }
}
