namespace Aw.Sindri.Ui.Testing;

/// <summary>A todo write the fake was sent.</summary>
/// <param name="Route">The path, such as /sindri/todos/td_x/done.</param>
/// <param name="Body">The body record the app sent (TodoAdd, TodoChange or TodoVersion).</param>
internal sealed record TodoWriteSent(string Route, object Body);
