using System.Globalization;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The instance frame's Model picks table (model-pick spec §6.1): one row per run, a total line and the escalation footer. Every word is a served field mapped to the app's copy.</summary>
internal sealed class ModelPicksViewModel
{
    private static readonly ModelPicksViewModel _none = new();

    /// <summary>One row per run, oldest first.</summary>
    public IReadOnlyList<ModelPickRowViewModel> Rows { get; private init; } = [];

    /// <summary>True when there is any run to list.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>The disclosure's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Heading => Labels.ModelPicksHeading;

    /// <summary>The column headers.</summary>
    public IReadOnlyList<string> Columns => Labels.ModelPickColumns;

    /// <summary>The Why panel's title.</summary>
    public string WhyTitle => Labels.WhyPanelTitle;

    /// <summary>The Why panel's candidates heading.</summary>
    public string CandidatesHeading => Labels.WhyCandidates;

    /// <summary>The Why panel's files heading.</summary>
    public string FilesHeading => Labels.WhyFiles;

    /// <summary>The Why panel's close button.</summary>
    public string CloseText => Labels.WhyClose;
#pragma warning restore CA1822, MA0041

    /// <summary>"3 runs, 96k tokens, $0.61", or with "cost partial: 1 model has no price".</summary>
    public string TotalLine { get; private init; } = string.Empty;

    /// <summary>"Write questions for you on Haiku: retried on a stronger model in 3 of 20 runs", one per state with history.</summary>
    public IReadOnlyList<string> EscalationLines { get; private init; } = [];

    /// <summary>"choosing model…" while any run is being picked, else empty (the live region's text).</summary>
    public string Announcement { get; private init; } = string.Empty;

    /// <summary>What the table draws, so a re-read of the same keeps an open flyout open.</summary>
    internal string Signature => string.Join('|', Rows.Select(r => r.Name + "/" + r.Why), TotalLine, string.Join(';', EscalationLines));

    /// <summary>Reads the served picks.</summary>
    /// <param name="view">The picks as served; null when the read failed or the server predates them.</param>
    /// <param name="stepName">A state id as the Step's display name.</param>
    /// <param name="clipboard">Where the Why panel's Copy writes.</param>
    /// <param name="clock">Times the Copy's "Copied".</param>
    /// <returns>The view model; empty without picks.</returns>
    internal static ModelPicksViewModel Of(LaunchPicksView? view, Func<string, string> stepName, IClipboard clipboard, TimeProvider clock)
    {
        if (view is not { Picks.Count: > 0 })
        {
            return _none;
        }

        var rows = view.Picks.Select(p => Row(p, stepName, clipboard, clock)).ToList();
        var settled = view.Picks.Where(p => !p.Picking).ToList();
        var unpriced = settled.Where(p => p.CostUsd is null).Select(p => p.Pick?.Model ?? Labels.AgentDefaultModel).Distinct(StringComparer.Ordinal).Count();
        var total = Labels.PicksTotal(settled.Count, settled.Sum(p => p.InputTokens + p.OutputTokens), settled.Sum(p => p.CostUsd ?? 0m), unpriced);
        var footer = (view.Escalation ?? []).Where(e => e.Runs > 0)
            .Select(e => $"{stepName(e.State)} on {Labels.ModelShortName(e.Model)}: {e.Sentence}")
            .ToList();
        return new ModelPicksViewModel { Rows = rows, TotalLine = total, EscalationLines = footer, Announcement = view.Picks.Any(p => p.Picking) ? Labels.ChoosingModel : string.Empty };
    }

    private static ModelPickRowViewModel Row(LaunchPickRow pick, Func<string, string> stepName, IClipboard clipboard, TimeProvider clock)
    {
        var step = pick.Escalation > 0 ? string.Create(CultureInfo.InvariantCulture, $"{stepName(pick.State)} (retry {pick.Escalation})") : stepName(pick.State);
        if (pick.Picking)
        {
            return new ModelPickRowViewModel(step, Labels.ChoosingModel, string.Empty, string.Empty, Labels.AskingJev, string.Empty, IsPicking: true, IsRetry: pick.Escalation > 0,
                ModelName: "Model: " + Labels.ChoosingModel, Name: Join(step, Labels.ChoosingModel, Labels.AskingJev), Panel: null);
        }

        var model = Labels.ModelShortName(pick.Pick?.Model);
        var tokens = Labels.TokensShort(pick.InputTokens + pick.OutputTokens);
        var cost = pick.CostUsd is { } usd ? Labels.Dollars(usd) : Labels.CostUnknown;
        var name = Join(step, model, tokens + " tokens", cost, pick.Why, pick.Outcome);
        return new ModelPickRowViewModel(step, model, tokens, cost, pick.Why, pick.Outcome, IsPicking: false, IsRetry: pick.Escalation > 0, ModelName: Labels.ModelCellName(model, pick.Pick?.By ?? string.Empty), Name: name, Panel: Panel(pick.Pick, clipboard, clock));
    }

    private static string Join(params string[] parts) => string.Join(", ", parts.Where(p => p.Length > 0));

    private static WhyPanelViewModel? Panel(PickedView? pick, IClipboard clipboard, TimeProvider clock)
    {
        if (pick is null)
        {
            return null;
        }

        var command = pick.Decision is { Length: > 0 } decision ? "judge why " + decision : string.Empty;
        return new WhyPanelViewModel
        {
            Candidates = pick.Candidates,
            Files = [.. pick.Files.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.Path} · {f.Chars} characters"))],
            Omitted = [.. pick.Omitted.Select(o => $"not shown: {o.Path}, {Labels.OmittedWhy(o.Why)}")],
            Command = command,
            CopyCommand = command.Length > 0 ? new CopyAction(clipboard, clock, "Copy the command " + command) : null,
        };
    }
}
