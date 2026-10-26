using System.Text.Json;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.EvaluateQuestion;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.EvaluateQuestion;

/// <summary>The service over its fakes: an eval store, a scripted jev, in-memory files and a redactor.</summary>
internal sealed class EvalWorld
{
    internal const string Question = "ask-check";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(JudgeJsonContext.Default);

    internal EvalWorld(IInputRedactor? redactor = null)
    {
        Service = new EvaluateQuestionService(Store, new FakeProviderCatalog(Jev), Files, redactor ?? new FakeInputRedactor(), Clock, _json);
    }

    internal FakeEvalStore Store { get; } = new();

    internal ScriptedProvider Jev { get; } = new(ProviderName.Jev);

    internal InMemoryEvalFiles Files { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    internal EvaluateQuestionService Service { get; }

    internal static string Input(string tail) => JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { ["transcriptTail"] = tail });

    internal EvalWorld Item(string id, string label, LabelSource source = LabelSource.Outcome)
    {
        _ = Store.Label(Question, id, Input("ok"), label, source);
        return this;
    }
}
