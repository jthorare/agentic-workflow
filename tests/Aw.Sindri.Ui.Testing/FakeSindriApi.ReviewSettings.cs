using Aw.Kernel;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The review settings calls of the fake (review page spec §7, Ph9): each answers the hand-written goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<string> _settingsReads = [];
    private readonly List<ReviewSettingsSave> _settingsSaves = [];

    /// <summary>GET /sindri/reviews/settings's answer: the "default-only" golden by default.</summary>
    public RouteAnswer ReviewSettingsAnswer { get; set; } = RouteAnswer.Api("review-settings", "default-only");

    /// <summary>PUT /sindri/reviews/settings's answer: the "approved" golden by default.</summary>
    public RouteAnswer ReviewSettingsSaveAnswer { get; set; } = RouteAnswer.Api("review-settings-save", "approved");

    /// <summary>When set, the save waits for it: the sheet stays on Saving until the test completes it.</summary>
    public TaskCompletionSource? ReviewSettingsSaveGate { get; set; }

    /// <summary>The projects whose settings were read, in order.</summary>
    public IReadOnlyList<string> ReviewSettingsReads { get { lock (_gate) { return [.. _settingsReads]; } } }

    /// <summary>The saves the sheet asked for, in order.</summary>
    public IReadOnlyList<ReviewSettingsSave> ReviewSettingsSaves { get { lock (_gate) { return [.. _settingsSaves]; } } }

    public Task<Result<ReviewSettingsRead>> GetReviewSettings(Uri api, string project, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _settingsReads.Add(project);
        }

        return Task.FromResult(Answer<ReviewSettingsRead>(api, ReviewRoutes.SettingsOf(project), ReviewSettingsAnswer));
    }

    public async Task<Result<ReviewSettingsSaved>> SaveReviewSettings(Uri api, ReviewSettingsSave request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _settingsSaves.Add(request);
        }

        await WaitFor(ReviewSettingsSaveGate, cancellationToken).ConfigureAwait(false);
        return Answer<ReviewSettingsSaved>(api, ReviewRoutes.Settings, ReviewSettingsSaveAnswer);
    }
}
