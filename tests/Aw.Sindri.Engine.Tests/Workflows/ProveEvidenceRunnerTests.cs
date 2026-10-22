using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflows.Inputs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'step.prove.evidence' as the runner hands it its facts (BS-5, LN7): its descriptor reads 'fact:proof' (#624, #670), so the proof the collect Step wrote reaches it.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveEvidenceRunnerTests
{
    private const string Head = "abcabcabcabcabcabc";
    private const string Address = "T-1/prove/1";

    private readonly InMemoryFactStore _store = new();
    private StepContext? _seen;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<Dictionary<FactKey, FactValue>> Facts()
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fact:phasePacket"] = """{"hash":"sha256:bbbb","dir":"/packets/p1"}""",
            ["fact:built"] = $$"""{"head":"{{Head}}"}""",
            ["fact:redTests"] = """{"AC-1":["T1"]}""",
            ["fact:beforeAfter"] = $$"""{"commit":"{{Head}}","results":[]}""",
            ["fact:holdoutResult"] = $$"""{"commit":"{{Head}}","canary":"clean","results":[{"scenario":"H1","item":"AC-1","verdict":"pass"},{"scenario":"H2","item":"AC-1","verdict":"pass"}]}""",
            ["fact:proof"] = $$"""{"head":"{{Head}}","items":[{"id":"AC-1","artifacts":[{"kind":"test","path":"e.txt","sha256":"sha256:aa","commit":"{{Head}}","at":"t","passing":true,"signal":"SG-1a"}]}]}""",
        };
        var facts = new Dictionary<FactKey, FactValue>();
        foreach (var (key, text) in texts)
        {
            facts[FactKey.From(key)] = (await _store.Put(Encoding.UTF8.GetBytes(text), Token)).Value;
        }

        return facts;
    }

    private BlockStepRunner Runner()
    {
        var files = new InMemoryBriefFiles()
            .With("acceptance.json", """{"schemaVersion":1,"parent":"sha256:aa","items":[{"id":"AC-1","text":"one","status":"failing","evidence":[]}]}""")
            .With("signals.json", """{"schemaVersion":1,"signals":{"AC-1":[{"id":"SG-1a","kind":"test","statement":"s1","runsAt":"head"}]}}""")
            .With("phase.json", """{"kind":"feature"}""")
            .With("holdout.json", """{"schemaVersion":1,"setHash":"sha256:aa","count":2}""")
            .With("packet.json", """{"schemaVersion":1,"kind":"phase","id":"line-p1","repo":"owner/name","baseSha":"1111111111111111111111111111111111111111","status":"frozen","hash":null,"links":{}}""");
        var step = new ProveEvidenceStep(files, new InMemoryRunArea(), new InMemoryPackets(), new InMemoryRepoCheckouts().With("name", "/checkouts/name"));
        return new BlockStepRunner(
            new BlockStepRuntimes([new BlockStepRegistration(ProveSteps.Evidence.Id, NeedsRepo: true)]),
            new BlockCatalog([ProveSteps.Evidence], []),
            (_, context, ct) =>
            {
                _seen = context;
                return step.Run(context, ct);
            },
            _store,
            _store,
            new InMemoryProfiles(),
            new StepContexts(new MemoryInjectionTexts(), new NoRunner(), new ItemReposStub()));
    }

    [Fact]
    public async Task The_Step_is_given_the_instances_fact_proof_and_completes()
    {
        var run = new BlockStepRun(Address, "prove", "1.1", ProveSteps.Evidence.Id, new Dictionary<string, WithValue>(StringComparer.Ordinal), await Facts(), "name");

        var outcome = await Runner().Run(run, Token);

        Assert.Contains(FactKey.From("fact:proof"), _seen!.Facts.Keys);   // the descriptor names it, so the runner reads it
        Assert.Null(outcome.Failure);
        Assert.Equal("complete", outcome.Outcome?.Value);
    }
}
