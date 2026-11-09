using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.CheckTodoNote;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.GetTodo;
using Aw.Sindri.Features.GetTodoNote;
using Aw.Sindri.Features.KeepTodo;
using Aw.Sindri.Features.KeepTodoUndo;
using Aw.Sindri.Features.ListTodos;
using Aw.Sindri.Features.MarkTodo;
using Aw.Sindri.Features.PickTodoHold;
using Aw.Sindri.Features.SetTodoNote;
using Aw.Sindri.Features.StopTodoNote;
using Aw.Sindri.Features.SyncTodoNote;
using Aw.Sindri.Features.UndoTodoHold;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
            .AddSingleton<TodoPickWindow>()
            .TryAddNoteSource()
            .AddScoped<TodoHolds>()
            .AddScoped<TodoConverger>()
            .AddScoped<TodoPickWrites>()
            .AddScoped<TodoAdds>()
            .AddScoped<TodoInput>()
            .AddScoped<IValidator<AddTodoRequest>, AcceptAllValidator<AddTodoRequest>>().AddCommand<AddTodoRequest, TodoAccepted, AddTodoService>()
            .AddScoped<IValidator<ChangeTodoRequest>, AcceptAllValidator<ChangeTodoRequest>>().AddCommand<ChangeTodoRequest, TodoAccepted, ChangeTodoService>()
            .AddScoped<IValidator<MarkTodoRequest>, AcceptAllValidator<MarkTodoRequest>>().AddCommand<MarkTodoRequest, TodoAccepted, MarkTodoService>()
            .AddScoped<IValidator<KeepTodoRequest>, AcceptAllValidator<KeepTodoRequest>>().AddCommand<KeepTodoRequest, KeepTodoResponse, KeepTodoService>()
            .AddScoped<IValidator<KeepTodoUndoRequest>, AcceptAllValidator<KeepTodoUndoRequest>>().AddCommand<KeepTodoUndoRequest, KeepTodoUndoResponse, KeepTodoUndoService>()
            .AddScoped<IValidator<PickTodoHoldRequest>, AcceptAllValidator<PickTodoHoldRequest>>().AddCommand<PickTodoHoldRequest, PickTodoHoldResponse, PickTodoHoldService>()
            .AddScoped<IValidator<UndoTodoHoldRequest>, AcceptAllValidator<UndoTodoHoldRequest>>().AddCommand<UndoTodoHoldRequest, UndoTodoHoldResponse, UndoTodoHoldService>()
            .AddScoped<IValidator<ListTodosRequest>, AcceptAllValidator<ListTodosRequest>>().AddQuery<ListTodosRequest, ListTodosResponse, ListTodosService>()
            .AddScoped<IValidator<GetTodoRequest>, AcceptAllValidator<GetTodoRequest>>().AddQuery<GetTodoRequest, GetTodoResponse, GetTodoService>()
            .AddScoped<IValidator<GetTodoNoteRequest>, AcceptAllValidator<GetTodoNoteRequest>>().AddQuery<GetTodoNoteRequest, GetTodoNoteResponse, GetTodoNoteService>()
            .AddScoped<IValidator<CheckTodoNoteRequest>, AcceptAllValidator<CheckTodoNoteRequest>>().AddCommand<CheckTodoNoteRequest, CheckTodoNoteResponse, CheckTodoNoteService>()
            .AddSingleton<INoteLocator, TodoNoteLocator>()
            .AddNoteSync()
            .AddScoped<IScheduledService, TodoNoteService>()
            .AddSingleton(TodoNoteService.BuiltIn)   // runs without a profile entry, so PauseService refuses it
            .AddScoped<TodoNoteProfileEdit>()
            .AddScoped<IValidator<SetTodoNoteRequest>, AcceptAllValidator<SetTodoNoteRequest>>().AddCommand<SetTodoNoteRequest, TodoNoteWritten, SetTodoNoteService>()
            .AddScoped<IValidator<StopTodoNoteRequest>, AcceptAllValidator<StopTodoNoteRequest>>().AddCommand<StopTodoNoteRequest, TodoNoteWritten, StopTodoNoteService>();
    }

    // why: the note service (todo Ph3b's sync PR) registers the real source before this runs or replaces it; until then no note is set
    private static IServiceCollection TryAddNoteSource(this IServiceCollection services)
    {
        services.TryAddSingleton<ITodoNoteSource, UnsetTodoNoteSource>();
        return services;
    }

    // why: the sync is one singleton (it serializes its passes); it builds its own TodoHolds, which only reads the ledger. ITodoNoteFiles and ITodoNoteDisk come from Infrastructure: the lead resolves the sync only where they are registered
    private static IServiceCollection AddNoteSync(this IServiceCollection services) =>
        services.AddSingleton<ITodoNoteSync>(static sp => new TodoNoteSync(new SyncDeps(
            sp.GetRequiredService<ITodoNoteFiles>(),
            sp.GetRequiredService<INoteLocator>(),
            sp.GetRequiredService<ITodoNoteDisk>(),
            sp.GetRequiredService<ILedgerStore>(),
            sp.GetRequiredService<HeldLease>(),
            sp.GetRequiredService<EventFactory>(),
            sp.GetRequiredService<IViewReader>(),
            new TodoHolds(sp.GetRequiredService<ILedgerStore>(), sp.GetRequiredService<EventFactory>()),
            sp.GetRequiredService<TodoPickWindow>(),
            sp.GetRequiredService<ITextScrubber>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IIdGenerator>(),
            sp.GetRequiredService<ILogger<TodoNoteSync>>())));
}
