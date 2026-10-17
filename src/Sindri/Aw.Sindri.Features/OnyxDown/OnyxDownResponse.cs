namespace Aw.Sindri.Features.OnyxDown;

/// <summary>What `sindri onyx down` did.</summary>
/// <param name="WasRunning">Some container of the project was running and has been stopped; false when nothing ran (already stopped, or never set up).</param>
/// <param name="Stopped">The services whose containers were running and are now stopped, ordinal order (empty when nothing ran).</param>
/// <param name="Warnings">Things worth telling the human that didn't stop the command, such as a recorded project name from another home that was ignored (empty when none).</param>
public sealed record OnyxDownResponse(bool WasRunning, IReadOnlyList<string> Stopped, IReadOnlyList<string> Warnings);
