using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.AddTodo;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/todos (todo spec §4.4): <see cref="AddTodoRequest"/>'s fields with the due date as a string ('yyyy-MM-dd', or absent) and the client id as text, so a bad one is a coded refusal.</summary>
/// <param name="Text">The text.</param>
/// <param name="Due">The due date as 'yyyy-MM-dd', or null; "" means none.</param>
/// <param name="Links">The typed links, or null.</param>
/// <param name="ClientId">The caller's retry id, or null.</param>
internal sealed record AddTodoBody(string? Text = null, string? Due = null, IReadOnlyList<string>? Links = null, string? ClientId = null)
{
    /// <summary>The request.</summary>
    /// <param name="piped">Whether the request came with fence=1.</param>
    /// <returns>The request, SND-TODO-001 for no text or a bad client id, or SND-TODO-003 for a bad date.</returns>
    internal Result<AddTodoRequest> ToRequest(bool piped)
    {
        if (Text is null)
        {
            return SindriErrors.TodoText.With("Add the todo's text.");
        }

        var due = TodoWire.Due(Due);
        if (due.IsError)
        {
            return due.Error;
        }

        var client = TodoWire.Client(ClientId);
        return client.IsError ? client.Error : new AddTodoRequest(Text, due.Value.Date, Links, client.Value, piped);
    }
}
