namespace Aw.Sindri.Core.Onyx;

/// <summary>Something about the managed Onyx the human has to act on, for the needs-you list.</summary>
/// <param name="Title">One line.</param>
/// <param name="Reason">Why, with no secret in it.</param>
/// <param name="Command">The command to run.</param>
/// <param name="Action">The catalog action the app offers instead ('SetupOnyx', or 'ConnectOnyx' while the setup waits for the login), or null when there is none.</param>
public sealed record OnyxNotice(string Title, string Reason, string Command, string? Action = null);
