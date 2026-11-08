using Aw.Kernel.Services;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.CheckTodoNote;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetTodo;
using Aw.Sindri.Features.GetTodoNote;
using Aw.Sindri.Features.ListTodos;
using Aw.Sindri.Features.MarkTodo;
using Aw.Sindri.Features.SetTodoNote;
using Aw.Sindri.Features.StopTodoNote;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the todo slices (todo spec §4.4): the three writes behind the command chain, the three reads behind the query chain, the note check (a command that writes nothing), and the two profile edits (routes behind the command chain; the CLI runs <see cref="TodoNoteProfileEdit"/> in process).</summary>
internal static class TodoServiceCollectionExtensions
{
    /// <summary>Adds AddTodo, ChangeTodo and MarkTodo (request class) and ListTodos, GetTodo and GetTodoNote.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddTodoSlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddSingleton<TodoAddWindow>()
            .AddScoped<TodoAdds>()
            .AddScoped<TodoInput>()
            .AddScoped<IValidator<AddTodoRequest>, AcceptAllValidator<AddTodoRequest>>().AddCommand<AddTodoRequest, TodoAccepted, AddTodoService>()
            .AddScoped<IValidator<ChangeTodoRequest>, AcceptAllValidator<ChangeTodoRequest>>().AddCommand<ChangeTodoRequest, TodoAccepted, ChangeTodoService>()
            .AddScoped<IValidator<MarkTodoRequest>, AcceptAllValidator<MarkTodoRequest>>().AddCommand<MarkTodoRequest, TodoAccepted, MarkTodoService>()
            .AddScoped<IValidator<ListTodosRequest>, AcceptAllValidator<ListTodosRequest>>().AddQuery<ListTodosRequest, ListTodosResponse, ListTodosService>()
            .AddScoped<IValidator<GetTodoRequest>, AcceptAllValidator<GetTodoRequest>>().AddQuery<GetTodoRequest, GetTodoResponse, GetTodoService>()
            .AddScoped<IValidator<GetTodoNoteRequest>, AcceptAllValidator<GetTodoNoteRequest>>().AddQuery<GetTodoNoteRequest, GetTodoNoteResponse, GetTodoNoteService>()
            .AddScoped<IValidator<CheckTodoNoteRequest>, AcceptAllValidator<CheckTodoNoteRequest>>().AddCommand<CheckTodoNoteRequest, CheckTodoNoteResponse, CheckTodoNoteService>()
            .AddScoped<TodoNoteProfileEdit>()
            .AddScoped<IValidator<SetTodoNoteRequest>, AcceptAllValidator<SetTodoNoteRequest>>().AddCommand<SetTodoNoteRequest, TodoNoteWritten, SetTodoNoteService>()
            .AddScoped<IValidator<StopTodoNoteRequest>, AcceptAllValidator<StopTodoNoteRequest>>().AddCommand<StopTodoNoteRequest, TodoNoteWritten, StopTodoNoteService>();
    }
}
