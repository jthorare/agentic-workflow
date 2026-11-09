namespace Aw.Sindri.Ui.Testing;

/// <summary>A conflict or hold pick, or a pick's Undo, the fake was sent.</summary>
/// <param name="Route">The path, such as /sindri/todos/td_x/keep.</param>
/// <param name="Body">The body record the app sent (TodoKeepBody or TodoHoldBody); null for an Undo, which sends none.</param>
internal sealed record TodoPickSent(string Route, object? Body);
