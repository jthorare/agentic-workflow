using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>What the Pin sheet reaches the control plane with: the API, the shared profile write flow, where the API is now and the clock for the search's debounce.</summary>
/// <param name="Api">Sindri's API.</param>
/// <param name="Flow">The write-then-approve flow.</param>
/// <param name="ApiUrl">The control plane's API address now, or null when none is known.</param>
/// <param name="Clock">The clock.</param>
internal sealed record PinSheetPorts(ISindriApi Api, ProfileWriteFlow Flow, Func<Uri?> ApiUrl, TimeProvider Clock);
