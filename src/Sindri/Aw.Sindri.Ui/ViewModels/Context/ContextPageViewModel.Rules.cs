using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The head's older-rules line (<see cref="ContextPageViewModel"/>, continued; #1153): the map status says the live map was built under older map rules, and the refresh is the way out.</summary>
internal sealed partial class ContextPageViewModel
{
    private const string RefreshAction = "RefreshContextMap";
    private WholeMapView? _rulesChecked;
    private bool _rulesOlder;

    /// <summary>The line by the head: the map was built under older map rules and a refresh rebuilds it.</summary>
    public string RulesOlderText { get; } = Labels.ContextRulesOlder;

    /// <summary>The refresh button (the catalog's RefreshContextMap), or null while the catalog has no such row.</summary>
    public ControlViewModel? RefreshControl { get; private set; }

    /// <summary>Whether the older-rules line shows: the whole map is on the stage and its status says the rules are older.</summary>
    public bool ShowRulesOlder => _rulesOlder && ShowWhole;

    /// <summary>Whether the refresh button shows beside the line.</summary>
    public bool ShowRulesRefresh => ShowRulesOlder && RefreshControl is not null;

    // Registered once: whenever the control plane is reachable and the whole map is drawn, its status is read once per answer (the whole map's own answer does not carry the rules).
    private void InitRules() => Ports.Follow(ReadRules);

    private async Task ReadRules(Uri api, CancellationToken cancellationToken)
    {
        if (Whole.Answer is not { } answer || ReferenceEquals(answer, _rulesChecked))
        {
            return;
        }

        _rulesChecked = answer;
        var status = await Ports.Api.MapStatus(api, anchor: null, cancellationToken).ConfigureAwait(true);
        _rulesOlder = status.IsSuccess && status.Value.RulesOlder;
        if (_rulesOlder)
        {
            Func<CancellationToken, Task<Result<CommandAcceptedView>>> send = StartRefresh;
            RefreshControl ??= Ports.Catalog.On(RefreshAction, subject: string.Empty, send);
        }

        OnChanged(nameof(ShowRulesOlder));
        OnChanged(nameof(ShowRulesRefresh));
    }

    // One press starts one refresh; the build overlay then follows the job like a first build, and the status is read again with the map it leaves.
    private async Task<Result<CommandAcceptedView>> StartRefresh(CancellationToken cancellationToken)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return SindriUiErrors.Unreachable.With("Sindri isn't reachable yet");
        }

        var started = await Ports.Api.RefreshContextMap(api, cancellationToken).ConfigureAwait(true);
        if (started.IsSuccess)
        {
            _buildJob = started.Value.JobId;
            IsBuilding = true;
            ChangedStates();
        }

        return started.Map(s => s.Accepted);
    }
}
