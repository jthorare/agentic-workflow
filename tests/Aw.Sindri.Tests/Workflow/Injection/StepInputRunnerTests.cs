using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflow.Injection;

/// <summary>
/// What shapes an input's output (plan decisions 2 and 7; engine conditions 4): the scrub runs before the digest, so the digest covers
/// what a Step sees; sections, the typed result and the plan are capped; names are checked; the caller's cancellation reaches the input.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StepInputRunnerTests
{
    private static readonly BlockId _input = BlockId.From("input.test.a");
    private static readonly BlockId _step = BlockId.From("step.test");
    private static readonly StepInputContext _context = new(_input, _step, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>());

    private sealed class SwapRedactor : IRedactor
    {
        public ScrubResult Scrub(string text) => new(text.Replace("sk-live-123", "[redacted]", StringComparison.Ordinal), [], ScrubOutcome.Redacted, BlockCode: null);

        public bool IsSecretName(string name) => false;
    }

    private static StepInputRunner Runner(Func<InjectionSections> fill, Action<CancellationToken>? seen = null) => new(
        (_, ct) =>
        {
            seen?.Invoke(ct);
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Kernel.Result<InjectionSections>>(fill());
        },
        new StepInputRuntimes([new StepInputRegistration(_input)]),
        new SwapRedactor());

    private static InjectionSections Sections(params (string Name, string Text)[] sections) =>
        new(sections.ToDictionary(static s => s.Name, static s => s.Text, StringComparer.Ordinal));

    [Fact]
    public async Task The_digest_covers_the_scrubbed_text_not_the_raw_text()
    {
        var runner = Runner(() => Sections(("notes", "token sk-live-123 here")));

        var filled = await runner.Fill(_context, 1, TestContext.Current.CancellationToken);

        Assert.Equal("token [redacted] here", filled.Value.Texts.Sections["notes"]);
        var entry = Assert.Single(filled.Value.Metadata.Sections);
        Assert.Equal(StepInputRunner.Sha("token [redacted] here"), entry.Sha256);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("token [redacted] here"), entry.Bytes);
    }

    [Fact]
    public async Task A_section_a_typed_result_and_a_plan_of_any_size_are_kept_whole()
    {
        var big = JsonSerializer.SerializeToElement(new string('b', 3 * 1024 * 1024));
        var runner = Runner(() => new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = new string('a', 2 * 1024 * 1024), ["b"] = new string('c', 2 * 1024 * 1024) }, big));

        var filled = await runner.Fill(_context, 1, TestContext.Current.CancellationToken);

        Assert.True(filled.IsSuccess, filled.IsError ? filled.Error.Message : null);
        Assert.Equal(2 * 1024 * 1024, filled.Value.Texts.Sections["a"].Length);
        Assert.Equal(3 * 1024 * 1024, filled.Value.Texts.Result!.Value.GetString()!.Length);
    }

    [Theory]
    [InlineData("Bad Name")]
    [InlineData("1abc")]
    [InlineData("")]
    public async Task A_section_name_outside_the_pattern_is_SND_WF_019(string name)
    {
        var runner = Runner(() => Sections((name, "x")));

        var filled = await runner.Fill(_context, 1, TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-019", filled.Error.Code);
    }

    [Fact]
    public async Task An_input_without_a_runtime_is_SND_WF_016()
    {
        var runner = new StepInputRunner((_, _) => throw new InvalidOperationException("must not run"), new StepInputRuntimes([]), new SwapRedactor());

        var filled = await runner.Fill(_context, 1, TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-016", filled.Error.Code);
    }

    [Fact]
    public async Task Fill_hands_the_callers_token_to_the_input_and_a_cancelled_call_ends_cancelled()
    {
        using var source = new CancellationTokenSource();
        CancellationToken given = default;
        var runner = Runner(() => Sections(("notes", "x")), ct => given = ct);
        await source.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await runner.Fill(_context, 1, source.Token));

        Assert.Equal(source.Token, given);
    }

    [Fact]
    public async Task An_inputs_failure_fails_the_fill_with_the_inputs_code_as_a_refill_mismatch_does()
    {
        var runner = new StepInputRunner(
            (_, _) => ValueTask.FromResult<Kernel.Result<InjectionSections>>(WorkflowErrors.InputChanged.With("fact:anchors changed: expected aaa, read bbb")),
            new StepInputRuntimes([new StepInputRegistration(_input)]),
            new SwapRedactor());

        var filled = await runner.Fill(_context, 1, TestContext.Current.CancellationToken);

        Assert.True(filled.IsError);
        Assert.Equal("SND-WF-018", filled.Error.Code);
        Assert.Contains("fact:anchors changed", filled.Error.Message, StringComparison.Ordinal);
    }
}
