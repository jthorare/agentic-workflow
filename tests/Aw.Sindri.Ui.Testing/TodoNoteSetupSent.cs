namespace Aw.Sindri.Ui.Testing;

/// <summary>One todo note setup call the fake received: the route and the body (null for a stop).</summary>
/// <param name="Route">The route.</param>
/// <param name="Body">The body as the client builds it.</param>
internal sealed record TodoNoteSetupSent(string Route, object? Body);
