namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The answer of GET /sindri/launches/{launch}/suspended (sandbox spec SB16, the engine's route): '{suspended}'.</summary>
/// <param name="Suspended">True while the run is parked or taken over.</param>
internal sealed record SuspendedAnswer(bool Suspended);
