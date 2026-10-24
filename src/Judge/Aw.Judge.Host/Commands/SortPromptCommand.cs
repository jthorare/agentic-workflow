using System.Text.Json;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.SortPrompt;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge prompt-sort' &lt; {prompt, sessionId}: the prompt-sort.sh hook path. The stdout is <see cref="SortPromptOutput"/>
/// only when a scaffold fires. It fails open: whatever goes wrong inside the sorter, the prompt is not the casualty (exit 0,
/// nothing printed); only input that isn't the hook's record is exit 1.
/// </summary>
internal static class SortPromptCommand
{
    internal static async Task<int> Run(IReadOnlyCollection<ProviderName>? allowed, IServiceProvider services, JsonSerializerOptions json)
    {
        var input = await CommandOutput.ReadInput<SortPromptInput>(json, CancellationToken.None).ConfigureAwait(false);
        if (input.IsError)
        {
            return CommandOutput.InvalidInput(input.Error.Message);
        }

        var session = !string.IsNullOrEmpty(input.Value.SessionId) && SessionId.TryFrom(input.Value.SessionId, out var id)
            ? id
            : services.GetRequiredService<CallingSession>().Id;
        var service = services.GetRequiredService<IService<SortPromptRequest, SortPromptResponse>>();
        try
        {
            var result = await service.Handle(new SortPromptRequest(input.Value.Prompt, session, allowed?.ToArray()), CancellationToken.None).ConfigureAwait(false);
            return result.IsError || result.Value.Context is null
                ? CommandOutput.Emit(0, "")
                : CommandOutput.Emit(0, CommandOutput.Json(new SortPromptOutput(result.Value.Context), json));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // why: a hook boundary; the sorter must never be the reason a prompt breaks.
            return CommandOutput.Emit(0, "");
        }
    }
}
