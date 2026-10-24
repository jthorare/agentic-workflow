using System.Text.Json;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.SortPrompt;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge prompt-sort' &lt; {prompt, sessionId}: the prompt-sort.sh hook path. Unit 6.1 builds the sorter behind it; the
/// stdout is <see cref="SortPromptOutput"/> only when a scaffold fires.
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
        var result = await service.Handle(new SortPromptRequest(input.Value.Prompt, session, allowed?.ToArray()), CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, result.Value.Context is null ? "" : CommandOutput.Json(new SortPromptOutput(result.Value.Context), json));
    }
}
