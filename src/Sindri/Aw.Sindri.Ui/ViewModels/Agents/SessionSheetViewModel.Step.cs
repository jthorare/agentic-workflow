using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The session sheet's Step (<see cref="SessionSheetViewModel"/>, continued): the instance, state and attempt an engine launch started the session for.</summary>
internal sealed partial class SessionSheetViewModel
{
    private static (string Label, string? Value)[] StepFields(SessionRowView session)
    {
        return
        [
            (Labels.SheetInstance, session.Launch?.Instance),
            (Labels.SheetStep, session.Launch?.State),
            (Labels.SheetAttempt, session.Launch?.Attempt is { } attempt ? Number(attempt) : null),
        ];
    }
}
