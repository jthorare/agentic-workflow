using Aw.Judge.Core.Config;
using Aw.Judge.Features.SetPromptSort;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.SetPromptSort;

// The promptSort block of config.json (cutover spec §5.2, unit 6.1): 'config set prompt-sort enabled' and
// 'prompt-sort scaffold <id> on|off' write it and keep every other key.
public sealed class SetPromptSortTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly InMemoryJudgeConfigStore _store = new()
    {
        Stored = JudgeConfig.Default with
        {
            Providers = new ProvidersConfig(["codex-cli"], Jev: false),
            PromptSort = new PromptSortConfig(Enabled: true, BudgetMs: 400, CooldownPrompts: 2, Scaffolds: new Dictionary<string, bool>(StringComparer.Ordinal) { ["brief"] = true }),
        },
    };

    [Fact, Trait("Lane", "unit")]
    public async Task SetPromptSort_sets_enabled_and_keeps_the_rest_of_the_config()
    {
        var result = await new SetPromptSortService(_store).Handle(A.SetPromptSortRequest().Enabled(false).Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("wake-gate=True/0.7/-; providers=[codex-cli]|False; promptSort=False|400|2|brief:True", ConfigText.Of(_store.Stored!));   // why: the service saved it
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SetPromptSort_switches_one_scaffold_and_keeps_the_others()
    {
        var result = await new SetPromptSortService(_store).Handle(A.SetPromptSortRequest().Scaffold("bugfix", on: true).Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new Dictionary<string, bool>(StringComparer.Ordinal) { ["brief"] = true, ["bugfix"] = true },
            _store.Stored!.PromptSort!.Scaffolds);   // why: the service saved a promptSort block
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SetPromptSort_refuses_a_scaffold_it_does_not_know_and_saves_nothing()
    {
        var before = _store.Stored;

        var result = await new SetPromptSortService(_store).Handle(A.SetPromptSortRequest().Scaffold("no-such-scaffold", on: true).Build(), _ct);

        Assert.True(result.IsError);
        Assert.Same(before, _store.Stored);
    }
}
