using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The head of one run's column.</summary>
internal sealed class CompareRunHeadViewModel
{
    private CompareRunHeadViewModel()
    {
        (Title, Facts, ChipWord, ChipGlyph, ToneWord, Result) = (string.Empty, string.Empty, string.Empty, string.Empty, "neutral", string.Empty);
    }

    internal CompareRunHeadViewModel(string letter, WorkflowRunView run, TimeProvider clock)
    {
        var local = TimeZoneInfo.ConvertTime(run.StartedAt, clock.LocalTimeZone);
        Title = $"Run {letter} \u00b7 " + Labels.WfDayTime(local);
        var parts = new List<string> { string.Create(CultureInfo.InvariantCulture, $"r{run.Revision}") };
        if (run.EndedAt is { } ended)
        {
            parts.Add(Labels.WfDuration(Math.Max(0, (ended - run.StartedAt).TotalSeconds)));   // a clock that stepped back never reads "-3 s"
        }

        parts.Add(Labels.WfRunTriggerWords(run.TriggerKind is { Length: > 0 } served ? served : KindOf(run.Trigger)));   // a CLI start is stored as manual: the served word tells it apart (#3683)
        Facts = string.Join(" \u00b7 ", parts);
        (ChipGlyph, ChipWord) = Labels.WfChip(run.Status);
        ToneWord = RunStatus.ToneWord(run.Status);
        Result = run.Error ?? string.Empty;
    }

    /// <summary>The head before any run is compared: every line empty.</summary>
    internal static CompareRunHeadViewModel None { get; } = new();

    /// <summary>"Run A \u00b7 Sat 14 Nov, 08:40".</summary>
    public string Title { get; }

    /// <summary>"r2 \u00b7 10 s \u00b7 From the CLI".</summary>
    public string Facts { get; }

    /// <summary>The run's status word.</summary>
    public string ChipWord { get; }

    /// <summary>The run's status glyph.</summary>
    public string ChipGlyph { get; }

    /// <summary>The chip tone word.</summary>
    public string ToneWord { get; }

    /// <summary>The run's one-line result.</summary>
    public string Result { get; }

    private static string KindOf(JsonElement? trigger) =>
        trigger is { ValueKind: JsonValueKind.Object } t && t.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String ? kind.GetString() ?? string.Empty : string.Empty;
}
