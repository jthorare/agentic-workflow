using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.GetTodo;
using Aw.Sindri.Features.GetTodoNote;
using Aw.Sindri.Features.ListTodos;
using Aw.Sindri.Features.MarkTodo;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the todo slices (todo spec §4.4). Red step: the services are stubs, registered as themselves; the ICommand and IQuery markers on the requests, the command catalog rows (ARCH-CQRS-003), the command and query chains and the endpoints come together with the implementation (Ph1a).</summary>
internal static class TodoServiceCollectionExtensions
{
    /// <summary>Adds AddTodo, ChangeTodo, MarkTodo, ListTodos, GetTodo and GetTodoNote.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddTodoSlices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<AddTodoService>()
            .AddScoped<ChangeTodoService>()
            .AddScoped<MarkTodoService>()
            .AddScoped<ListTodosService>()
            .AddScoped<GetTodoService>()
            .AddScoped<GetTodoNoteService>();
    }
}
